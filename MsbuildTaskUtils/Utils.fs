namespace MsbuildUtilityHelpers

open System.Xml
open System

type ICheckerLogger =
    abstract member ReportMessage : string -> unit
    abstract member ReportException : Exception -> unit

type Utils() = 
    let getEnvironmentVariable var = 
        System.Environment.GetEnvironmentVariable(var).Split(";".ToCharArray())

    member this.EscapeString(str : string) = 
        let doc = new XmlDocument()
        let node = doc.CreateElement("root")
        node.InnerText <- str
        node.InnerXml

    member this.ExistsOnPath(program) =
        let path = getEnvironmentVariable("PATH")
        let mutable returncode = false

        for i in path do
            let file = System.IO.Path.Combine(i, program)
            if System.IO.File.Exists(file) then
                returncode <- true
        returncode

    member this.ProcessFileUsingReplacementStrings (file : string, propertiestoreplace : string) = 
        if file.Contains("$(") then
            let values = propertiestoreplace.Split(';')
            let mutable fileend = file
            for elem in  values do
                if elem <> "" then
                    let key = elem.Split('=').[0]
                    let value = elem.Split('=').[1]
                    let replacestr = sprintf "$(%s)" key
                    fileend <- fileend.Replace(replacestr, value)

            fileend
        else
            file
        



