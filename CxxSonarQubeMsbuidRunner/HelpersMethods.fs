module HelpersMethods

open System
open System.Net
open System.Web
open System.IO
open System.Text

let cprintf(c : ConsoleColor, stringdata:string) = 

    let old = System.Console.ForegroundColor 
    try 
        System.Console.ForegroundColor <- c
        System.Console.WriteLine(stringdata)
    finally
        System.Console.ForegroundColor <- old
        
let GetRequest(username: string, password:string, url : string) =
    // required to call https://sonarqube.com 
    // not convinced on the scalability of this approach should Tls12 become vulnerable
    ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12

    printf "[SERVER CALL] %s\r\n" url
    let req = HttpWebRequest.Create(url) :?> HttpWebRequest 
    req.Method <- "GET"
    req.ContentType <- "text/json"

    if username <> "" then
        let pwd = 
            if String.IsNullOrWhiteSpace(password) then
                String.Empty
            else
                password
        let auth = "Basic " + (username + ":" + pwd |> Encoding.UTF8.GetBytes |> Convert.ToBase64String)
        req.Headers.Add("Authorization", auth)

    // read data
    let rsp = req.GetResponse()
    use stream = rsp.GetResponseStream()
    use reader = new StreamReader(stream)
    let timeNow = System.DateTime.Now.ToString()

    reader.ReadToEnd()
