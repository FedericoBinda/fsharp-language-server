namespace LSP 

open System
open System.IO
open System.Collections.Generic
open System.Text
open Types 

type private Version = {
    text: StringBuilder 
    mutable version: int
}

module DocumentStoreUtils = 
    let findRange (text: StringBuilder) (range: Range): int * int = 
        let mutable line = 0
        let mutable char = 0
        let mutable startOffset = 0
        let mutable endOffset = 0
        for offset = 0 to text.Length do 
            if line = range.start.line && char = range.start.character then 
                startOffset <- offset 
            if line = range._end.line && char = range._end.character then 
                endOffset <- offset 
            if offset < text.Length then 
                let c = text.[offset]
                if c = '\n' then 
                    line <- line + 1
                    char <- 0
                else 
                    char <- char + 1
        (startOffset, endOffset)

open DocumentStoreUtils

type DocumentStore() = 
    let compareUris = 
        { new IEqualityComparer<Uri> with 
            member this.Equals(x, y) = 
                StringComparer.CurrentCulture.Equals(x, y)
            member this.GetHashCode(x) = 
                StringComparer.CurrentCulture.GetHashCode(x) }
    let activeDocuments = new Dictionary<Uri, Version>(compareUris)
        
    let patch (doc: VersionedTextDocumentIdentifier) (range: Range) (text: string): unit = 
        let existing = activeDocuments.[doc.uri]
        let startOffset, endOffset = findRange existing.text range
        existing.text.Remove(startOffset, endOffset - startOffset) |> ignore
        existing.text.Insert(startOffset, text) |> ignore
        existing.version <- doc.version
    let replace (doc: VersionedTextDocumentIdentifier) (text: string): unit = 
        let existing = activeDocuments.[doc.uri]
        existing.text.Clear() |> ignore
        existing.text.Append(text) |> ignore
        existing.version <- doc.version

    member this.Open(doc: DidOpenTextDocumentParams): unit = 
        let text = StringBuilder(doc.textDocument.text)
        let version = {text = text; version = doc.textDocument.version}
        activeDocuments.[doc.textDocument.uri] <- version

    member this.Change(doc: DidChangeTextDocumentParams): unit = 
        let existing = activeDocuments.[doc.textDocument.uri]
        if doc.textDocument.version <= existing.version then 
            eprintfn
                "Change %d to doc %s is earlier than existing version %d" 
                doc.textDocument.version 
                (doc.textDocument.uri.ToString())
                existing.version
        else 
            for change in doc.contentChanges do 
                match change.range with 
                | Some range -> patch doc.textDocument range change.text 
                | None -> replace doc.textDocument change.text 

    member this.GetText(uri: Uri): option<string> = 
        let found, value = activeDocuments.TryGetValue(uri)
        if found then Some (value.text.ToString()) else None 

    member this.GetVersion(uri: Uri): option<int> = 
        let found, value = activeDocuments.TryGetValue(uri)
        if found then Some value.version else None 

    member this.Close(doc: DidCloseTextDocumentParams): unit = 
        activeDocuments.Remove doc.textDocument.uri |> ignore