module Generators.Formatting

open System
open FSharp.Reflection
open Newtonsoft.Json.Linq
open Conversion

module String =

    let private escapeSpecialCharacters(str: string) =
        str.Replace("\n", "\\n")
           .Replace("\t", "\\t")
           .Replace("\r", "\\r")
           .Replace("\"", "\\\"")

    let render (str: string) =
        str
        |> escapeSpecialCharacters
        |> String.enquote

module Bool =    

    let render b = if b then "true" else "false"

module DateTime =

    let render (dateTime: DateTime) = 
        if dateTime.TimeOfDay = TimeSpan.Zero then
            sprintf "DateTime(%d, %d, %d)" dateTime.Year dateTime.Month dateTime.Day
        else
            sprintf "DateTime(%d, %d, %d, %d, %d, %d)" dateTime.Year dateTime.Month dateTime.Day dateTime.Hour dateTime.Minute dateTime.Second

    let renderParenthesized (dateTime: DateTime) = 
        dateTime
        |> render
        |> String.parenthesize

let renderJToken (jToken: JToken) =
    match jToken.Type with
    | JTokenType.Integer when JToken.isInt64 jToken -> jToken.ToObject<int64>() |> string
    | JTokenType.Integer  -> jToken.ToObject<int>()      |> string
    | JTokenType.Float    -> jToken.ToObject<float>()    |> string
    | JTokenType.Boolean  -> jToken.ToObject<bool>()     |> Bool.render
    | JTokenType.String   -> jToken.ToObject<string>()   |> String.render
    | JTokenType.Date     -> jToken.ToObject<DateTime>() |> DateTime.render
    | _ -> string jToken

let renderTuple tuple = sprintf "%A" tuple

let renderRecord record = sprintf "%A" record

let rec renderObj (value: obj) =
    let rec renderJArray (jArray: JArray) =
        jArray
        |> Seq.map renderObj
        |> String.concat "; "
        |> sprintf "[%s]"

    match value with
    | :? string as s -> 
        String.render s
    | :? bool as b -> 
        Bool.render b
    | :? DateTime as dateTime -> 
        DateTime.render dateTime
    | :? JArray as jArray -> 
        renderJArray jArray
    | :? JToken as jToken -> 
        renderJToken jToken
    | _ when FSharpType.IsTuple (value.GetType()) -> 
        renderTuple value
    | _ when FSharpType.IsRecord (value.GetType()) -> 
        renderRecord value
    | _ -> 
        string value

let private renderCollection formatString collection =
    collection
    |> String.concat "; "
    |> sprintf formatString

let private renderMultiLineCollection (openPrefix, closePostfix) collection indentation =
    match Seq.length collection with
    | 0 -> 
        sprintf "%s%s" openPrefix closePostfix
    | 1 -> 
        sprintf "%s%s%s" openPrefix (Seq.head collection) closePostfix    
    | length -> 
        let lineIndent = String(' ', String.length openPrefix)

        let formatLine i line = 
            match i with
            | 0 -> 
                sprintf "%s %s;" openPrefix line
            | _ when i = length - 1 -> 
                sprintf "%s %s %s" lineIndent line closePostfix
            | _ ->
                sprintf "%s %s;" lineIndent line

        collection
        |> Seq.mapi formatLine
        |> Seq.toList
        |> List.map (String.indent indentation)
        |> String.concat "\n"
        |> sprintf "\n%s"

module Option =

    let private renderMap valueMap someMap option =
        match option with
        | None -> "None"
        | Some value -> sprintf "Some %s" (valueMap value) |> someMap

    let renderString option = renderMap id id option

    let renderStringParenthesized option = renderMap id String.parenthesize option

    let render option = renderMap renderObj id option

    let renderParenthesized option = renderMap renderObj String.parenthesize option

module List =

    let renderStrings value = renderCollection "[%s]" value

    let renderMultiLineStrings value = renderMultiLineCollection ("[", "]") value 2

    let render value = 
        value
        |> List.map renderObj
        |> renderStrings

    let renderMultiLine value =
        value
        |> List.map renderObj
        |> renderMultiLineStrings

module Array =

    let renderStrings value = renderCollection "[|%s|]" value

module Obj =

    let render value = renderObj value

    let renderEnum typeName value = 
        let enumType = String.upperCaseFirst typeName
        let enumValue = String.dehumanize (string value)
        sprintf "%s.%s" enumType enumValue   
