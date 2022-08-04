namespace pilipala.pipeline.comment

open System
open System.Collections.Generic
open fsharper.op
open fsharper.typ
open fsharper.op.Alias
open fsharper.op.Pattern
open fsharper.op.Foldable
open pilipala.access.user
open pilipala.id
open pilipala.data.db
open pilipala.pipeline
open pilipala.pipeline.comment
open pilipala.container.comment

module ICommentInitPipelineBuilder =
    let make () =
        let inline gen () =
            { collection = List<PipelineCombineMode<'I, 'O>>()
              beforeFail = List<'I -> 'I>() }

        { new ICommentInitPipelineBuilder with
            member i.Batch = gen () }

type CommentInitPipeline internal (initBuilder: ICommentInitPipelineBuilder, db: IDbOperationBuilder) =

    let data (comment: CommentData) =
        let bind_id, comment_is_reply =
            match comment.Binding with
            | BindPost post_id -> post_id, false
            | BindComment comment_id -> comment_id, true

        let fields: (_ * obj) list =
            [ ("comment_id", comment.Id)
              ("bind_id", bind_id)
              ("comment_body", comment.Body)
              ("comment_create_time", comment.CreateTime)
              ("comment_is_reply", comment_is_reply)
              ("user_id", comment.UserId)
              ("user_permission", comment.Permission) ]

        let aff =
            db {
                inComment
                insert fields
                whenEq 1
                execute
            }

        if aff = 1 then
            //TODO 可以添加Item迭代器，以实现在Init管道的udf初始化
            (*            for KV (name, v) in comment do
                ()
*)
            Some comment.Id
        else
            None

    member self.Batch =
        initBuilder.Batch.fullyBuild
        <| fun fail x -> unwrapOr (data x) (fun _ -> fail x)
