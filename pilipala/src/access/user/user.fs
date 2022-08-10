namespace pilipala.access.user

open System
open fsharper.op
open fsharper.typ
open fsharper.alias
open pilipala.id
open pilipala.data.db
open pilipala.util.hash
open pilipala.container.post
open pilipala.container.comment

type User
    (
        palaflake: IPalaflakeGenerator,
        mappedPostProvider: IMappedPostProvider,
        mappedCommentProvider: IMappedCommentProvider,
        mappedUserProvider: IMappedUserProvider,
        mapped: IMappedUser,
        db: IDbOperationBuilder
    ) =
    member self.Id = mapped.Id

    member self.ReadPostPermissionLv =
        (mapped.Permission &&& 3072us) >>> 10

    member self.WritePostPermissionLv =
        (mapped.Permission &&& 300us) >>> 8

    member self.ReadCommentPermissionLv =
        (mapped.Permission &&& 192us) >>> 6

    member self.WriteCommentPermissionLv =
        (mapped.Permission &&& 48us) >>> 4

    member self.ReadUserPermissionLv =
        (mapped.Permission &&& 12us) >>> 2

    member self.WriteUserPermissionLv =
        mapped.Permission &&& 3us

    member self.Name
        with get () = mapped.Name
        and set v = mapped.Name <- v

    member self.Email
        with get () = mapped.Email
        and set v = mapped.Email <- v

    member self.CreateTime
        with get () = mapped.CreateTime
        and set v = mapped.CreateTime <- v

    member self.AccessTime
        with get () = mapped.AccessTime
        and set v = mapped.AccessTime <- v

    member self.Permission
        with get () = mapped.Permission
        and set v = mapped.Permission <- v

    member self.Item
        with get name = mapped.[name]
        and set name v = mapped.[name] <- v

    member self.NewPost(title, body) =
        if self.WritePostPermissionLv <> 0us then
            { Id = palaflake.next ()
              Title = title
              Body = body
              CreateTime = DateTime.Now
              AccessTime = DateTime.Now
              ModifyTime = DateTime.Now
              UserId = mapped.Id
              Permission =
                let r = 00uy //可见性默认为00

                r
                ||| u8 (mapped.Permission &&& 12us) //从用户继承的修改权
                ||| r //可评论性与可见性默认相同
              Item = always None }
            |> mappedPostProvider.create
            |> fun x -> Post(palaflake, x, mappedCommentProvider, mapped)
            |> Ok
        else
            Err "Permission denied"

    member self.GetPost id : Result'<Post, string> =
        if db {
            inComment
            getFstVal "post_id" "post_id" id
            execute
        } = None then
            Err "Invalid post id"
        else
            Post(palaflake, mappedPostProvider.fetch id, mappedCommentProvider, mapped)
            |> Ok

    member self.GetComment id =
        if db {
            inComment
            getFstVal "comment_id" "comment_id" id
            execute
        } = None then
            Err "Invalid comment id"
        else
            Comment(palaflake, mappedCommentProvider.fetch id, mappedCommentProvider, mapped)
            |> Ok

    member self.NewUser(name, pwd: string, permission) =
        if self.WriteUserPermissionLv >= 2us then //TODO，暂不作实现，仅限pl_register(wu级别2)及root(wu级别3)访问，借助于该验证，子账户系统是可期望的
            if db {
                inUser
                getFstVal "user_name" "user_name" name
                execute
               }
               <> None then
                Err "Username already exists"
            else
                { Id = palaflake.next ()
                  Name = name
                  Email = "" //应由用户自行绑定
                  CreateTime = DateTime.Now
                  AccessTime = DateTime.Now
                  Permission = permission //TODO，暂不作实现，推荐的权限级别为337(commentator)
                  Item = always None }
                |> mappedUserProvider.create
                |> fun x ->
                    let aff =
                        db {
                            inUser
                            update "user_pwd_hash" pwd.bcrypt "user_id" x.Id
                            whenEq 1
                            execute
                        }

                    if aff <> 1 then //非期望行为，let it crash
                        failwith $"Initialize user pwd failed (affected:{aff})"

                    User(palaflake, mappedPostProvider, mappedCommentProvider, mappedUserProvider, x, db)
                |> Ok
        else
            Err "Permission denied"

    member self.GetUser id =
        if self.ReadUserPermissionLv >= 2us then //TODO，暂不作实现，仅限pl_register(ru级别2)及root(ru级别3)访问
            if db {
                inUser
                getFstVal "user_id" "user_id" id
                execute
            } = None then
                Err "Invalid user id"
            else
                User(
                    palaflake,
                    mappedPostProvider,
                    mappedCommentProvider,
                    mappedUserProvider,
                    mappedUserProvider.fetch id,
                    db
                )
                |> Ok
        else
            Err "Permission denied"

    member inline private self.GetPostGen(mask: u8) =
        Seq.unfold
        <| fun list ->
            match list with
            | x :: xs ->
                let post =
                    Post(palaflake, mappedPostProvider.fetch (coerce x), mappedCommentProvider, mapped)

                Option.Some(post, xs)
            | _ -> Option.None
        <| db {
            getFstCol
                $"SELECT post_id FROM {db.tables.post} \
                  WHERE user_id = {mapped.Id} \
                  OR ({mapped.Permission} & {mask}) > (post_permission & {mask})"
                []

            execute
        }

    member self.GetReadablePost() = self.GetPostGen(48uy)
    member self.GetWritablePost() = self.GetPostGen(12uy)
    member self.GetCommentablePost() = self.GetPostGen(3uy)

    member inline private self.GetCommentGen(mask: u8) =
        Seq.unfold
        <| fun list ->
            match list with
            | x :: xs ->
                let comment =
                    Comment(palaflake, mappedCommentProvider.fetch (coerce x), mappedCommentProvider, mapped)

                Option.Some(comment, xs)
            | _ -> Option.None
        <| db {
            getFstCol
                $"SELECT comment_id FROM {db.tables.comment} \
                  WHERE user_id = {mapped.Id} \
                  OR ({mapped.Permission} & {mask}) > (comment_permission & {mask})"
                []

            execute
        }

    member self.GetReadableComment() = self.GetCommentGen(48uy)
    member self.GetWritableComment() = self.GetCommentGen(12uy)
    member self.GetCommentableComment() = self.GetCommentGen(3uy)
