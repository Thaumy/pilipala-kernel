namespace pilipala.pipeline.post

open System.Collections.Generic
open fsharper.typ
open fsharper.typ.Pipe
open fsharper.op.Alias
open fsharper.op.Error
open fsharper.op.Foldable
open DbManaged
open DbManaged.PgSql
open pilipala.id
open pilipala.data.db
open pilipala.pipeline
open pilipala.container.post

module IPostInitPipelineBuilder =
    let make () =
        let inline gen () =
            { collection = List<PipelineCombineMode<'I, 'O>>()
              beforeFail = List<IGenericPipe<'I, 'I>>() }

        { new IPostInitPipelineBuilder with
            member i.Batch = gen () }

type PostInitPipeline
    internal
    (
        initBuilder: IPostInitPipelineBuilder,
        palaflake: IPalaflakeGenerator,
        db: IDbOperationBuilder,
        user_group: u8
    ) =
    let data (post: IPost) =
        let post_id = palaflake.next ()

        let fields: (_ * obj) list =
            [ ("post_id", post_id)
              ("post_title", post.Title)
              ("post_body", post.Body)
              ("post_create_time", post.CreateTime)
              ("post_access_time", post.AccessTime)
              ("post_modify_time", post.AccessTime)
              ("user_group", user_group) ]

        let aff =
            db {
                inPost
                insert fields
                whenEq 1
            }
            |> db.managed.executeQuery

        if aff = 1 then
            Some(post_id, post)
        else
            None

    let gen (initBuilderItem: BuilderItem<_, _>) =
        let beforeFail =
            initBuilderItem.beforeFail.foldr (fun p (acc: IPipe<_>) -> acc.export p) (Pipe<_>())

        let fail = beforeFail.fill .> panicwith

        initBuilderItem.collection.foldl
        <| fun acc x ->
            match x with
            | Before before -> before.export acc
            | Replace f -> f acc
            | After after -> acc.export after
        <| GenericCachePipe<_, _>(data, fail)

    member self.Batch = gen initBuilder.Batch
