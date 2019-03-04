namespace CheckAndSend.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open SaveAndSend.Logic
open SaveAndSend.Config
open System.IO

[<Route("api/[controller]")>]
[<ApiController>]
type HotelChangeController (config: IConfiguration) =
    inherit ControllerBase()
    let cfg = (config.GetSection "ParserConfig").Get<Config>()

    [<HttpGet>]
    member this.Get() =
        
        try
            let changes = HotelsLoader.sendMailIfChanged cfg
            Ok(changes)
        with
        | _ as ex ->
            //logger.LogError(ex, ex.Message)
            Error(ex)

    [<HttpGet>]
    [<Route("config")>]
    member this.GetCfg() =
        //logger.LogInformation (sprintf "%A" cfg)
        try
            Ok(cfg)
        with
        | _ as ex -> Error ex

    
    [<HttpGet>]
    [<Route("path")>]
    member this.GetPath() =
        //logger.LogInformation (sprintf "%A" cfg)
        try
            Ok(FileInfo("").FullName)
        with
        | _ as ex -> Error ex