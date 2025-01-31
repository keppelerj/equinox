﻿[<AutoOpen>]
module Domain.Infrastructure

open FSharp.UMX
open Newtonsoft.Json
open System

/// Endows any type that inherits this class with standard .NET comparison semantics using a supplied token identifier
[<AbstractClass>]
type Comparable<'TComp, 'Token when 'TComp :> Comparable<'TComp, 'Token> and 'Token : comparison>(token : 'Token) =
    member private _.Token = token
    override x.Equals y = match y with :? Comparable<'TComp, 'Token> as y -> x.Token = y.Token | _ -> false
    override _.GetHashCode() = hash token
    interface IComparable with
        member x.CompareTo y =
            match y with
            | :? Comparable<'TComp, 'Token> as y -> compare x.Token y.Token
            | _ -> invalidArg "y" "invalid comparand"

/// Endows any type that inherits this class with standard .NET comparison semantics using a supplied token identifier
/// + treats the token as the canonical rendition for `ToString()` purposes
[<AbstractClass>]
type StringId<'TComp when 'TComp :> Comparable<'TComp, string>>(token : string) =
    inherit Comparable<'TComp,string>(token)
    override _.ToString() = token

module Guid =
    let inline toStringN (x : Guid) = x.ToString "N"

/// SkuId strongly typed id
/// - Ensures canonical rendering without dashes via ToString + Newtonsoft.Json
/// - Guards against XSS by only permitting initialization based on Guid.Parse
/// - Implements comparison/equality solely to enable tests to leverage structural equality
[<Sealed; AutoSerializable(false); JsonConverter(typeof<SkuIdJsonConverter>); System.Text.Json.Serialization.JsonConverter(typeof<SkuIdJsonConverterStj>)>]
type SkuId private (id : string) =
    inherit StringId<SkuId>(id)
    new(value : Guid) = SkuId(value.ToString "N")
    /// Required to support empty
    [<Obsolete>] new() = SkuId(Guid.NewGuid())
/// Represent as a Guid.ToString("N") output externally
and private SkuIdJsonConverter() =
    inherit FsCodec.NewtonsoftJson.JsonIsomorphism<SkuId, string>()
    /// Renders as per `Guid.ToString("N")`, i.e. no dashes
    override _.Pickle value = string value
    /// Input must be a `Guid.Parse`able value
    override _.UnPickle input = Guid.Parse input |> SkuId
and private SkuIdJsonConverterStj() =
    inherit FsCodec.SystemTextJson.JsonIsomorphism<SkuId, string>()
    override _.Pickle value = string value
    override _.UnPickle input = Guid.Parse input |> SkuId

/// RequestId strongly typed id, represented internally as a string
/// - Ensures canonical rendering without dashes via ToString, Newtonsoft.Json, sprintf "%s" etc
/// - using string enables one to lean on structural equality for types embedding one
type RequestId = string<requestId>
and [<Measure>] requestId
module RequestId =
    /// - For web inputs, should guard against XSS by only permitting initialization based on RequestId.parse
    /// - For json deserialization where the saved representation is not trusted to be in canonical Guid form,
    ///     it is recommended to bind to a Guid and then upconvert to string<requestId>
    let parse (value : Guid<requestId>) : string<requestId> = % Guid.toStringN %value

/// CartId strongly typed id; represented internally as a Guid; not used for storage so rendering is not significant
type CartId = Guid<cartId>
and [<Measure>] cartId
module CartId = let toString (value : CartId) : string = Guid.toStringN %value

/// ClientId strongly typed id; represented internally as a Guid; not used for storage so rendering is not significant
type ClientId = Guid<clientId>
and [<Measure>] clientId
module ClientId = let toString (value : ClientId) : string = Guid.toStringN %value

/// InventoryItemId strongly typed id
type InventoryItemId = Guid<inventoryItemId>
and [<Measure>] inventoryItemId
module InventoryItemId = let toString (value : InventoryItemId) : string = Guid.toStringN %value

module EventCodec =

    /// For CosmosStore - we encode to JsonElement as that's what the store talks
    let genJsonElement<'t when 't :> TypeShape.UnionContract.IUnionContract> =
        FsCodec.SystemTextJson.CodecJsonElement.Create<'t>()

    /// For stores other than CosmosStore, we encode to UTF-8 and have the store do the right thing
    let gen<'t when 't :> TypeShape.UnionContract.IUnionContract> =
        FsCodec.NewtonsoftJson.Codec.Create<'t>()
