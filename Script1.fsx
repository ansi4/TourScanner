open System.Collections.Generic

#r "C:/Users/Alex/.nuget/packages/FSharp.Data/3.0.0/lib/net45/FSharp.data.dll"
#I __SOURCE_DIRECTORY__


open FSharp.Data
open System.IO
open System.Net.Mail

let [<Literal>] url = "https://www.itaka.pl/wyniki-wyszukiwania/narty?date-from=2019-02-09&date-to=2019-02-16&package-type=narty&adults=2&dep-region=warszawa"
let [<Literal>] baseApiUrl = "https://www.itaka.pl/api_www/get/last_rooms_free"
let [<Literal>] apiUrl = baseApiUrl + "?ofr_id=99c766f7f24f51d390245ab465eb62f5bcd8a0f1f18932025c3d85bbbd2f6521&adults=2&childs=0&currency=PLN"

type ResultsProvider = HtmlProvider<url>
type RoomCountProvider = JsonProvider<apiUrl>
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
type Config = {
    Hotels: seq<string>
    TempPath: string
    SerpUrl: string
    BaseApiUrl: string
    MailServer: string
    Sender: string
    Receiver: string
    Subject: string
}

let serp = ResultsProvider()
let hotels = ["TRNPLAN"; "TRNPILA"]
let innerText = (fun (html: HtmlNode) -> html.InnerText())
let getJsonUrl = (fun baseUrl (url: string) -> baseUrl + url.Substring(url.IndexOf('?')))
let getRoomCnt = (fun (url: string) -> RoomCountProvider.Load(url) |> (fun js -> js.Data.Result.MinimalRooms))

let getHotels = fun (cfg: Config) -> 
    ResultsProvider.Load(cfg.SerpUrl).Html.Descendants "article" |> 
        Seq.filter (fun el -> cfg.Hotels |> Seq.exists (fun h -> el.HasAttribute("data-code", h))) |>
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

let checkFileExists = File.Exists
let loadHotels = fun path -> 
    if checkFileExists path 
        then (CsvFileProvider.Load path).Rows |> Seq.map (fun h -> (h.Name, {Name = h.Name; RoomCount = h.Rooms; Price = h.Price})) |> dict
        else Dictionary<string, Hotel>() :> (IDictionary<string, Hotel>)

let saveHotels = fun (path: string) (hotels: seq<Hotel>) -> 
    hotels |> Seq.map (fun h -> CsvFileProvider.Row(h.Name, h.RoomCount, h.Price)) |>
        (fun rows -> using(new CsvFileProvider(rows)) (fun csv -> csv.Save(path)))

let compareHotels = fun (old: IDictionary<string, Hotel>) (updated: IDictionary<string, Hotel>) -> {
    Added = updated |> Seq.filter (fun n -> not (old.ContainsKey n.Key)) |> Seq.map (fun r -> r.Value);
    Removed = old |> Seq.filter (fun o -> not (updated.ContainsKey o.Key)) |> Seq.map (fun r -> r.Value);
    Changed = old |> Seq.filter (fun o -> updated.ContainsKey o.Key) |> Seq.map (fun o -> (o.Value, updated.[o.Key]))
}

let formatHotel = fun ({Name=n; RoomCount=r; Price=p}) ->
    sprintf "<li>%s with %i rooms for %i PLN</li>" n r p
let formatChange = fun ({Name=n; RoomCount=oldCnt; Price=oldPrc}, {Name=n; RoomCount=r; Price=p}) ->
    sprintf "<li>%s changed: <ul>%s%s</ul></li>" 
        n 
        (if oldCnt <> r then sprintf "<li>room count from <strong>%i</strong> to <strong>%i</strong></li>" oldCnt r else "")
        (if oldCnt <> r then sprintf "<li>price from <strong>%i</strong>PLN to <strong>%i</strong>PLN</li>" oldPrc p else "")
let formatMessageBody = fun (chng: Changes) ->
    sprintf "<article><h1>The following items have changed:</h1>%s %s %s</article>"
        (if chng.Added |> Seq.isEmpty then "" else sprintf "<h2>Added:</h2><ul>%s</ul>" (chng.Added |> Seq.map formatHotel |> String.concat ""))
        (if chng.Removed |> Seq.isEmpty then "" else sprintf "<h2>Removed:</h2><ul>%s</ul>" (chng.Removed |> Seq.map formatHotel |> String.concat ""))
        (if chng.Changed |> Seq.isEmpty then "" else sprintf "<h2>Changed:</h2><ul>%s</ul>" (chng.Changed |> Seq.map formatChange |> String.concat ""))
let sendMail = fun server message ->
    use client = new SmtpClient(server)
    client.DeliveryMethod <- SmtpDeliveryMethod.Network
    client.Send message
let sendMessage = fun sendFunction sender receiver subject body ->
    use message = new MailMessage(sender, receiver, subject, body)
    sendFunction message

let compareToOldHotels = fun cfg -> compareHotels (loadHotels cfg.TempPath) (getHotels cfg)
let sendMailIfChanged = fun cfg -> 
    let changes = compareToOldHotels cfg
    if (changes.Added |> Seq.isEmpty |> not) 
        || (changes.Removed |> Seq.isEmpty |> not)
        || (changes.Changed |> Seq.isEmpty |> not)
        then changes |> formatMessageBody |> sendMessage (sendMail cfg.MailServer) cfg.Sender cfg.Receiver cfg.Subject
        else changes |> ignore