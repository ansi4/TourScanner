namespace SaveAndSend.Logic

open FSharp.Data
open System.IO
open System.Collections.Generic
open System.Net
open System.Net.Mail
open SaveAndSend.Config

module HotelsLoader =

    type ResultsProvider = HtmlProvider<"sample\sample.html">
    type RoomCountProvider = JsonProvider<"sample\sample_json.json">
    type CsvFileProvider = CsvProvider<"name,5,1000;", Schema = "Name (string), Rooms (int), Price (int)", HasHeaders = false>

    type Hotel = {
        Name: string
        RoomCount: int
        Price: int
    }
    type Changes = {
        Added: seq<Hotel>
        Removed: seq<Hotel>
        Changed: seq<Hotel * Hotel>
    }


    let innerText = (fun (html: HtmlNode) -> html.InnerText())
    let getJsonUrl = (fun baseUrl (url: string) -> baseUrl + url.Substring(url.IndexOf('?')))
    let getRoomCnt = (fun (url: string) -> 
        try 
            RoomCountProvider.Load(url) |> (fun js -> js.Data.Result.MinimalRooms)
        with
        | _ as ex -> 0
    )

    let getHotels = fun (cfg: Config) -> 
        ResultsProvider.Load(cfg.SerpUrl).Html.Descendants "article" |> 
            Seq.map (
                fun h -> 
                (
                    h.Descendants "header" |> 
                        Seq.exactlyOne |> (
                            fun el -> {
                                Name = el.Descendants "h2" |> Seq.exactlyOne |> innerText;
                                RoomCount = 
                                    el.Elements "a" |> 
                                        Seq.exactlyOne |> 
                                        (fun a -> a.AttributeValue("href") |> getJsonUrl cfg.BaseApiUrl |> getRoomCnt);
                                Price = 
                                    h.Descendants "span" |> 
                                        Seq.filter (fun el -> el.HasClass("current-price_value")) |> 
                                        Seq.exactlyOne |> 
                                        innerText |> 
                                        String.filter (System.Char.IsDigit) |> 
                                        (int)
                            }
                        )
                ) 
            ) |>
            Seq.map (fun h -> (h.Name, h))
            |> dict

    let getFile = fun path -> if File.Exists path then Some(FileInfo(path)) else None 
    let loadHotels = getFile >> function  
        | None -> Dictionary<string, Hotel>() :> (IDictionary<string, Hotel>)
        | Some(file) -> 
            use csvFileStream = new StreamReader(file.FullName)
            let res = (CsvFileProvider.Load csvFileStream).Rows |> 
                Seq.map (fun h -> (h.Name, {Name = h.Name; RoomCount = h.Rooms; Price = h.Price})) |>
                dict
            csvFileStream.Close()
            res
            

    let saveHotels = fun (path: string) (hotels: IDictionary<string, Hotel>) -> 
        hotels |> Seq.map (fun (KeyValue (_, h)) -> CsvFileProvider.Row(h.Name, h.RoomCount, h.Price)) |>
            (fun rows -> using(new CsvFileProvider(rows)) (fun csv -> csv.Save(path)))
        hotels

    let compareHotels = fun (old: IDictionary<string, Hotel>) (updated: IDictionary<string, Hotel>) -> {
        Added = updated |> Seq.filter (fun n -> not (old.ContainsKey n.Key)) |> Seq.map (fun r -> r.Value);
        Removed = old |> Seq.filter (fun o -> not (updated.ContainsKey o.Key)) |> Seq.map (fun r -> r.Value);
        Changed = old |> 
            Seq.filter (fun o -> updated.ContainsKey o.Key) |>
            Seq.map (fun o -> (o.Value, updated.[o.Key])) |>
            Seq.filter (fun (o, n) -> o.Price <> n.Price || o.RoomCount <> n.RoomCount)
    }

    let formatHotel = fun ({Name=n; RoomCount=r; Price=p}) ->
        sprintf "<li>%s with %i rooms for %i PLN</li>" n r p
    let formatChange = fun ({Name=n; RoomCount=oldCnt; Price=oldPrc}, {Name=n; RoomCount=r; Price=p}) ->
        sprintf "<li>%s changed: <ul>%s%s</ul></li>" 
            n 
            (if oldCnt <> r then sprintf "<li>room count from <strong>%i</strong> to <strong>%i</strong></li>" oldCnt r else "")
            (if oldPrc <> p then sprintf "<li>price from <strong>%i</strong>PLN to <strong>%i</strong>PLN</li>" oldPrc p else "")
    let formatMessageBody = fun (chng: Changes) ->
        sprintf "<article><h1>The following items have changed:</h1>%s %s %s</article>"
            (if chng.Added |> Seq.isEmpty then "" else sprintf "<h2>Added:</h2><ul>%s</ul>" (chng.Added |> Seq.map formatHotel |> String.concat ""))
            (if chng.Removed |> Seq.isEmpty then "" else sprintf "<h2>Removed:</h2><ul>%s</ul>" (chng.Removed |> Seq.map formatHotel |> String.concat ""))
            (if chng.Changed |> Seq.isEmpty then "" else sprintf "<h2>Changed:</h2><ul>%s</ul>" (chng.Changed |> Seq.map formatChange |> String.concat ""))
    let sendMail = fun (cfg: Config) message ->
        use client = new SmtpClient(cfg.MailServer, cfg.MailPort)
        client.DeliveryMethod <- SmtpDeliveryMethod.Network
        client.EnableSsl <- true
        client.Credentials <- NetworkCredential(cfg.Sender, cfg.EmailPassword)
        client.Send message
    let sendMessage = fun sendFunction (sender: string) (receiver: string) subject body ->
        use message = new MailMessage()
        message.From <- MailAddress sender
        message.Subject <- subject
        message.Body <- body
        message.IsBodyHtml <- true
        receiver.Split "; " |> Array.iter message.To.Add
        sendFunction message

    let compareToOldHotels = fun (cfg: Config) -> compareHotels (loadHotels cfg.TempPath) (getHotels cfg |> saveHotels cfg.TempPath)
    let sendMailIfChanged = fun (cfg: Config) -> 
        let changes = compareToOldHotels cfg
        if (changes.Added |> Seq.isEmpty |> not) 
            || (changes.Removed |> Seq.isEmpty |> not)
            || (changes.Changed |> Seq.isEmpty |> not)
            then changes |> formatMessageBody |> sendMessage (sendMail cfg) cfg.Sender cfg.Receiver cfg.Subject
            else changes |> ignore
        changes