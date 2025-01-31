﻿namespace Equinox.SqlStreamStore.Postgres

open Equinox.Core
open SqlStreamStore

type Connector
    (    connectionString: string, [<O; D(null)>]?schema: string,
         [<O; D(null)>]?readRetryPolicy, [<O; D(null)>]?writeRetryPolicy,
         /// <c>true</c> to auto-create the schema upon connection
         [<O; D(null)>]?autoCreate) =
    inherit Equinox.SqlStreamStore.ConnectorBase(?readRetryPolicy=readRetryPolicy,?writeRetryPolicy=writeRetryPolicy)

    let settings = PostgresStreamStoreSettings(connectionString)
    do match schema with Some x when (not << System.String.IsNullOrWhiteSpace) x -> settings.Schema <- x | _ -> ()

    let store = new PostgresStreamStore(settings)

    override _.Connect() = async {
        if autoCreate = Some true then do! store.CreateSchemaIfNotExists() |> Async.AwaitTaskCorrect
        return store :> IStreamStore
    }
