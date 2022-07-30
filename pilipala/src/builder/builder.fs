﻿namespace pilipala.builder

open Microsoft.Extensions.DependencyInjection
open fsharper.typ.Pipe
open pilipala
open pilipala.container.comment
open pilipala.container.post
open pilipala.id
open pilipala.log
open pilipala.serv
open pilipala.plugin
open pilipala.pipeline.post
open pilipala.pipeline.comment

type Builder = { pipeline: IPipe<IServiceCollection> }

(*
    内核构造序：
    useDb
    usePlugin
    useAuth
    useLog
    useLog
    useLog
    useService
    useService
    useService
    usePostCache
    useCommentCache
    *)

type Builder with

    member self.build(serverId) =
        let f (sc: IServiceCollection) =
            sc
                .AddSingleton<LogProvider>()
                .AddSingleton<ServProvider>()
                .AddSingleton<PluginProvider>()
                //ID生成服务
                .AddSingleton<IPalaflakeGenerator>(fun _ -> IPalaflakeGenerator.make serverId)
                .AddSingleton<IUuidGenerator>(fun _ -> IUuidGenerator.make ())
                //文章管道构造器
                .AddSingleton<IPostInitPipelineBuilder>(fun _ -> IPostInitPipelineBuilder.make ())
                .AddSingleton<IPostRenderPipelineBuilder>(fun _ -> IPostRenderPipelineBuilder.make ())
                .AddSingleton<IPostModifyPipelineBuilder>(fun _ -> IPostModifyPipelineBuilder.make ())
                .AddSingleton<IPostFinalizePipelineBuilder>(fun _ -> IPostFinalizePipelineBuilder.make ())
                //文章管道
                .AddTransient<PostRenderPipeline>()
                .AddTransient<PostModifyPipeline>()
                .AddTransient<PostFinalizePipeline>()
                //文章提供器
                .AddTransient<IPostProvider>(fun sf ->
                    IPostProvider.make (
                        sf.GetService<PostInitPipeline>(),
                        sf.GetService<PostRenderPipeline>(),
                        sf.GetService<PostModifyPipeline>(),
                        sf.GetService<PostFinalizePipeline>()
                    ))
                //评论管道构造器
                .AddSingleton<ICommentInitPipelineBuilder>(fun _ -> ICommentInitPipelineBuilder.make ())
                .AddSingleton<ICommentRenderPipelineBuilder>(fun _ -> ICommentRenderPipelineBuilder.make ())
                .AddSingleton<ICommentModifyPipelineBuilder>(fun _ -> ICommentModifyPipelineBuilder.make ())
                .AddSingleton<ICommentFinalizePipelineBuilder>(fun _ -> ICommentFinalizePipelineBuilder.make ())
                //评论管道
                .AddTransient<CommentRenderPipeline>()
                .AddTransient<CommentModifyPipeline>()
                .AddTransient<CommentFinalizePipeline>()
                //评论提供器
                .AddTransient<ICommentProvider>(fun sf ->
                    ICommentProvider.make (
                        sf.GetService<CommentInitPipeline>(),
                        sf.GetService<CommentRenderPipeline>(),
                        sf.GetService<CommentModifyPipeline>(),
                        sf.GetService<CommentFinalizePipeline>()
                    ))

        StatePipe(activate = f)
            .export(self.pipeline)
            .export(StatePipe(activate = fun sc -> sc.AddSingleton<Pilipala>()))
            .fill(ServiceCollection())
            .BuildServiceProvider()
            .GetService<Pilipala>()
