module LSP.LanguageServerTests

open System
open System.IO 
open System.Text
open NUnit.Framework
open FSharp.Data
open LSP.Types

let binaryWriter () = 
    let stream = new MemoryStream()
    let writer = new BinaryWriter(stream)
    let toString () = 
        let bytes = stream.ToArray() 
        Encoding.UTF8.GetString bytes
    (writer, toString)

[<Test>]
let ``write text``() = 
    let (writer, toString) = binaryWriter() 
    writer.Write (Encoding.UTF8.GetBytes "foo")
    Assert.That(toString(), Is.EqualTo "foo")

[<Test>]
let ``write response``() = 
    let (writer, toString) = binaryWriter() 
    LanguageServer.respond writer 1 "2"
    let expected = "Content-Length: 19\r\n\r\n\
                    {\"id\":1,\"result\":2}"
    Assert.That(toString(), Is.EqualTo expected)

[<Test>]
let ``write multibyte characters``() = 
    let (writer, toString) = binaryWriter() 
    LanguageServer.respond writer 1 "🔥"
    let expected = "Content-Length: 22\r\n\r\n\
                    {\"id\":1,\"result\":🔥}"
    Assert.That(toString(), Is.EqualTo expected)


let TODO() = raise (Exception "TODO")

type MockServer() = 
    interface ILanguageServer with 
        member this.Initialize(p: InitializeParams): InitializeResult = 
            { capabilities = defaultServerCapabilities }
        member this.Initialized(): unit = TODO() 
        member this.Shutdown(): unit = TODO() 
        member this.DidChangeConfiguration(p: DidChangeConfigurationParams): unit  = TODO()
        member this.DidOpenTextDocument(p: DidOpenTextDocumentParams): unit  = TODO()
        member this.DidChangeTextDocument(p: DidChangeTextDocumentParams): unit  = TODO()
        member this.WillSaveTextDocument(p: WillSaveTextDocumentParams): unit = TODO()
        member this.WillSaveWaitUntilTextDocument(p: WillSaveTextDocumentParams): list<TextEdit> = TODO()
        member this.DidSaveTextDocument(p: DidSaveTextDocumentParams): unit = TODO()
        member this.DidCloseTextDocument(p: DidCloseTextDocumentParams): unit = TODO()
        member this.DidChangeWatchedFiles(p: DidChangeWatchedFilesParams): unit = TODO()
        member this.Completion(p: TextDocumentPositionParams): CompletionList = TODO()
        member this.Hover(p: TextDocumentPositionParams): Hover = TODO()
        member this.ResolveCompletionItem(p: CompletionItem): CompletionItem = TODO()
        member this.SignatureHelp(p: TextDocumentPositionParams): SignatureHelp = TODO()
        member this.GotoDefinition(p: TextDocumentPositionParams): list<Location> = TODO()
        member this.FindReferences(p: ReferenceParams): list<Location> = TODO()
        member this.DocumentHighlight(p: TextDocumentPositionParams): list<DocumentHighlight> = TODO()
        member this.DocumentSymbols(p: DocumentSymbolParams): list<SymbolInformation> = TODO()
        member this.WorkspaceSymbols(p: WorkspaceSymbolParams): list<SymbolInformation> = TODO()
        member this.CodeActions(p: CodeActionParams): list<Command> = TODO()
        member this.CodeLens(p: CodeLensParams): List<CodeLens> = TODO()
        member this.ResolveCodeLens(p: CodeLens): CodeLens = TODO()
        member this.DocumentLink(p: DocumentLinkParams): list<DocumentLink> = TODO()
        member this.ResolveDocumentLink(p: DocumentLink): DocumentLink = TODO()
        member this.DocumentFormatting(p: DocumentFormattingParams): list<TextEdit> = TODO()
        member this.DocumentRangeFormatting(p: DocumentRangeFormattingParams): list<TextEdit> = TODO()
        member this.DocumentOnTypeFormatting(p: DocumentOnTypeFormattingParams): list<TextEdit> = TODO()
        member this.Rename(p: RenameParams): WorkspaceEdit = TODO()
        member this.ExecuteCommand(p: ExecuteCommandParams): unit = TODO()

let messageStream (messages: list<string>): BinaryReader = 
    let stdin = new MemoryStream()
    for m in messages do 
        let length = Encoding.UTF8.GetByteCount m 
        let wrapper = sprintf "Content-Length: %d\r\n\r\n%s" length m 
        let bytes = Encoding.UTF8.GetBytes wrapper 
        stdin.Write(bytes, 0, bytes.Length)
    stdin.Seek(int64 0, SeekOrigin.Begin) |> ignore
    new BinaryReader(stdin)

let initializeMessage = """
{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {}
}
"""

[<Test>]
let ``read messages from a stream``() = 
    let stdin = messageStream [initializeMessage]
    let messages = LanguageServer.readMessages stdin
    Assert.That(messages, Is.EquivalentTo [Parser.RequestMessage (1, "initialize", JsonValue.Parse "{}")])

let exitMessage = """
{
    "jsonrpc": "2.0",
    "method": "exit"
}
"""
    
[<Test>]
let ``exit message terminates stream``() = 
    let stdin = messageStream [initializeMessage; exitMessage; initializeMessage]
    let messages = LanguageServer.readMessages stdin
    Assert.That(messages, Is.EquivalentTo [Parser.RequestMessage (1, "initialize", JsonValue.Parse "{}")])

let mock (server: ILanguageServer) (messages: list<string>): string = 
    let stdout = new MemoryStream()
    let writeOut = new BinaryWriter(stdout)
    let readIn = messageStream messages
    LanguageServer.connect server readIn writeOut
    Encoding.UTF8.GetString(stdout.ToArray())

[<Test>]
let ``send Initialize``() = 
    let message = """
    {
        "jsonrpc": "2.0",
        "id": 1,
        "method": "initialize",
        "params": {"processId": null,"rootUri":null,"capabilities":{}}
    }
    """
    let server = MockServer()
    let result = mock server [message]
    Assert.That(result, Contains.Substring "capabilities")