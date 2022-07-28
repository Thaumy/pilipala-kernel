namespace pilipala.data.db

open System.Data.Common
open DbManaged
open DbManaged.PgSql
open fsharper.typ
open fsharper.typ.Ord

module IDbOperationBuilder =
    let make (config: DbConfig) =
        let msg =
            { host = config.connection.host
              port = config.connection.port
              usr = config.connection.usr
              pwd = config.connection.pwd
              db = config.connection.using }

        { new IDbOperationBuilder with
            member i.managed =
                new PgSqlManaged(msg, config.pooling.size)

            member i.tables = config.map

            member i.makeCmd() = i.managed.mkCmd () }
