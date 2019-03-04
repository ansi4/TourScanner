// Learn more about F# at http://fsharp.org

open System
open FSharp.Data;

let [<Literal>] url = "https://www.itaka.pl/wyniki-wyszukiwania/narty?date-from=2019-02-09&date-to=2019-02-16&package-type=narty&adults=2&dep-region=warszawa"
type provider = HtmlProvider<url>

[<EntryPoint>]
let main argv =
    let serp = provider()
    //printf "%A" (serp.Html.Body().DescendantsWithPath "//article" |> Seq.map (fun art -> art.))
    System.Console.ReadKey()
    0 // return an integer exit code
