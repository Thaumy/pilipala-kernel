namespace pilipala.container.Post

open fsharper.op.Alias
open pilipala.pipeline
open System
open fsharper.op
open fsharper.typ
open fsharper.typ.Ord
open fsharper.op.Alias
open pilipala
open pilipala.util
open pilipala.container
open DbManaged.PgSql.ext.String
open System
open fsharper.op
open fsharper.typ
open fsharper.typ.Ord
open fsharper.op.Alias
open pilipala
open pilipala.util
open pilipala.util.hash
open pilipala.container
open DbManaged.PgSql.ext.String

type MutPost internal (postId: u64) =

    let meta_entry = post_meta_entry (postId)

    let record_entry =
        post_record_entry (meta_entry.bindRecordId)

    member self.postId = postId

    /// 创建时间
    member self.ctime
        with get (): DateTime = meta_entry.ctime
        and set (v: DateTime) = meta_entry.ctime <- v
    /// 访问时间
    member self.atime
        with get (): DateTime = meta_entry.atime
        and set (v: DateTime) = meta_entry.atime <- v
    /// 修改时间
    member self.mtime
        with get (): DateTime = record_entry.mtime
        and set (v: DateTime) = record_entry.mtime <- v
    /// 访问数
    member self.view
        with get (): u32 = meta_entry.view
        and set (v: u32) = meta_entry.view <- v
    /// 星星数
    member self.star
        with get (): u32 = meta_entry.star
        and set (v: u32) = meta_entry.star <- v
    /// 封面
    member self.cover
        with get (): string = record_entry.cover
        and set (v: string) = record_entry.cover <- v
    /// 标题
    member self.title
        with get (): string = record_entry.title
        and set (v: string) = record_entry.title <- v
    /// 概述
    member self.summary
        with get (): string = record_entry.summary
        and set (v: string) = record_entry.summary <- v
    /// 正文
    member self.body
        with get (): string = record_entry.body
        and set (v: string) = record_entry.body <- v
