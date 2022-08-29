namespace pilipala.pipeline.comment

open System.Collections.Generic
open fsharper.op
open fsharper.typ
open fsharper.op.Pattern
open pilipala.data.db
open pilipala.pipeline
open pilipala.pipeline.comment
open pilipala.container.comment

module ICommentInitPipelineBuilder =
    let make () =
        let inline gen () =
            { collection = List<_>()
              beforeFail = List<_>() }

        let batch = gen ()

        { new ICommentInitPipelineBuilder with
            member i.Batch = batch }

module ICommentInitPipeline =
    let make (initBuilder: ICommentInitPipelineBuilder, db: IDbOperationBuilder) =
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
                Some comment.Id
            else
                None

        let batch =
            initBuilder.Batch.fullyBuild
            <| fun fail x -> unwrapOr (data x) (fun _ -> fail x)

        { new ICommentInitPipeline with
            member self.Batch a = batch a }