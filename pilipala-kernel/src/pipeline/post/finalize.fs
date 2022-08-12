namespace pilipala.pipeline.post

open System.Collections.Generic
open fsharper.op
open fsharper.typ
open fsharper.alias
open fsharper.op.Pattern
open pilipala.data.db
open pilipala.pipeline
open pilipala.container.post

//TODO 考虑为Builder引入计算表达式
module IPostFinalizePipelineBuilder =
    let make () =

        let inline gen () =
            { collection = List<_>()
              beforeFail = List<_>() }

        let batch = gen ()

        { new IPostFinalizePipelineBuilder with
            member i.Batch = batch }

module IPostFinalizePipeline =
    let make
        (
            renderBuilder: IPostRenderPipelineBuilder,
            finalizeBuilder: IPostFinalizePipelineBuilder,
            db: IDbOperationBuilder
        ) =

        let udf_render_no_after = //去除了After部分的udf渲染管道，因为After可能包含渲染逻辑
            Dict<_, _>()
            |> effect (fun dict ->
                for KV (name, builderItem) in renderBuilder do //遍历udf
                    //udf管道初始为只会panic的GenericPipe，必须Replace后使用
                    dict.Add(name, builderItem.noneAfterBuild id))

        let data (post_id: i64) =
            let db_data =
                db {
                    inPost
                    getFstRow "post_id" post_id
                    execute
                }
                |> unwrap

            if db {
                inPost
                delete "post_id" post_id
                whenEq 1
                execute
            } = 1 then
                { Id = post_id //回送被删除的文章
                  Title = coerce db_data.["post_title"]
                  Body = coerce db_data.["post_body"]
                  CreateTime = coerce db_data.["post_create_time"]
                  AccessTime = coerce db_data.["post_access_time"]
                  ModifyTime = coerce db_data.["post_modify_time"]
                  UserId = coerce db_data.["user_id"]
                  Permission = coerce db_data.["post_permission"]
                  Item = //只读
                    fun name ->
                        udf_render_no_after.TryGetValue(name).intoOption'()
                            .fmap
                        <| (apply ..> snd) post_id }
                |> Some
            else
                None

        let batch =
            finalizeBuilder.Batch.fullyBuild
            <| fun fail id -> unwrapOr (data id) (fun _ -> fail id)

        { new IPostFinalizePipeline with
            member i.Batch a = batch a }
