Option Explicit

Dim fso, shell, appDir, dataDir, baseUrl, wantOpen, port
Set fso = CreateObject("Scripting.FileSystemObject")
Set shell = CreateObject("WScript.Shell")

appDir = fso.GetParentFolderName(WScript.ScriptFullName)
dataDir = shell.ExpandEnvironmentStrings("%LOCALAPPDATA%") & "\Atlas"
port = ReadPort()
baseUrl = "http://127.0.0.1:" & port
wantOpen = False

If WScript.Arguments.Count > 0 Then
    wantOpen = (LCase(WScript.Arguments(0)) = "open")
End If

If Not IsRunning() Then
    shell.CurrentDirectory = appDir
    shell.Run """" & appDir & "\atlas.exe"" --data-dir """ & dataDir & """ --urls http://127.0.0.1:" & port, 0, False
End If

If wantOpen Then
    WaitForServer
    shell.Run baseUrl, 1, False
End If

Function IsRunning()
    Dim wmi
    Set wmi = GetObject("winmgmts:\\.\root\cimv2")
    IsRunning = (wmi.ExecQuery("SELECT ProcessId FROM Win32_Process WHERE Name = 'atlas.exe'").Count > 0)
End Function

Function ReadPort()
    Dim value
    value = ""

    On Error Resume Next
    value = shell.RegRead("HKCU\Software\Atlas\Port")
    If Err.Number <> 0 Then
        Err.Clear
        value = ""
    End If
    On Error GoTo 0

    value = Trim(value)
    If Not IsNumeric(value) Then
        ReadPort = "5000"
    Else
        ReadPort = CStr(CLng(value))
    End If
End Function

Sub WaitForServer()
    Dim http, i
    Set http = CreateObject("WinHttp.WinHttpRequest.5.1")
    http.SetTimeouts 1000, 1000, 1000, 2000

    For i = 1 To 40
        WScript.Sleep 500
        On Error Resume Next
        http.Open "GET", baseUrl & "/", False
        http.Send
        If Err.Number = 0 Then
            If http.Status = 200 Then Exit Sub
        End If
        Err.Clear
        On Error GoTo 0
    Next
End Sub
