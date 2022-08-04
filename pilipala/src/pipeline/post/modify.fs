namespace pilipala.pipeline.post

open System
open System.Collections
open System.Collections.Generic
open fsharper.op
open fsharper.typ
open fsharper.typ.Pipe
open fsharper.op.Alias
open fsharper.op.Pattern
open fsharper.op.Foldable
open pilipala.data.db
open pilipala.pipeline

module IPostModifyPipelineBuilder =
    let make () =
        let inline gen () =
            { collection = List<PipelineCombineMode<'I, 'O>>()
              beforeFail = List<'I -> 'I>() }

        let udf = //user defined field
            Dict<string, BuilderItem<u64 * obj>>()

        { new IPostModifyPipelineBuilder with
            member i.Title = gen ()
            member i.Body = gen ()
            member i.CreateTime = gen ()
            member i.AccessTime = gen ()
            member i.ModifyTime = gen ()
            member i.UserId = gen ()
            member i.Permission = gen ()

            member i.Item name =
                if udf.ContainsKey name then
                    udf.[name]
                else
                    let x = gen ()
                    udf.Add(name, x)
                    x

            member i.GetEnumerator() : IEnumerator = udf.GetEnumerator()

            member i.GetEnumerator() : IEnumerator<_> = udf.GetEnumerator() }

type PostModifyPipeline internal (modifyBuilder: IPostModifyPipelineBuilder, db: IDbOperationBuilder) =
    let set targetKey (idVal: u64, targetVal) =
        match
            db {
                inPost
                update targetKey targetVal "post_id" idVal
                whenEq 1
                execute
            }
            with
        | 1 -> Some(idVal, targetVal)
        | _ -> None

    let udf =
        Dict<string, u64 * obj -> u64 * obj>()

    do
        for KV (name, builderItem) in modifyBuilder do
            udf.Add(name, builderItem.fullyBuild id)

    member self.Title =
        modifyBuilder.Title.fullyBuild
        <| fun fail x -> unwrapOr (set "post_title" x) (fun _ -> fail x)

    member self.Body =
        modifyBuilder.Body.fullyBuild
        <| fun fail x -> unwrapOr (set "post_body" x) (fun _ -> fail x)

    member self.CreateTime =
        modifyBuilder.CreateTime.fullyBuild
        <| fun fail x -> unwrapOr (set "post_create_time" x) (fun _ -> fail x)

    member self.AccessTime =
        modifyBuilder.AccessTime.fullyBuild
        <| fun fail x -> unwrapOr (set "post_access_time" x) (fun _ -> fail x)

    member self.ModifyTime =
        modifyBuilder.ModifyTime.fullyBuild
        <| fun fail x -> unwrapOr (set "post_modify_time" x) (fun _ -> fail x)

    member self.UserId =
        modifyBuilder.UserId.fullyBuild
        <| fun fail x -> unwrapOr (set "user_id" x) (fun _ -> fail x)

    member self.Permission =
        modifyBuilder.Permission.fullyBuild
        <| fun fail x -> unwrapOr (set "post_permission" x) (fun _ -> fail x)

    member self.Item(name: string) = udf.TryGetValue(name).intoOption' ()
