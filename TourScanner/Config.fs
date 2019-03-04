namespace SaveAndSend
module Config =
    type Config () =
        member val TempPath: string = "" with get, set
        member val SerpUrl: string = "" with get, set
        member val BaseApiUrl: string = "" with get, set
        member val MailServer: string = "" with get, set
        member val MailPort: int = 80 with get, set
        member val Sender: string = "" with get, set
        member val EmailPassword: string = "" with get, set
        member val Receiver: string = "" with get, set
        member val Subject: string = "" with get, set
    