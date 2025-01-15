// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

#region Regex Suppressions
[assembly: SuppressMessage("Performance", "SYSLIB1045:In „GeneratedRegexAttribute“ konvertieren.", Justification = "<Ausstehend>", Scope = "member", Target = "~M:Server_Messenger.Logging.Logger.LogInformation``1(System.ConsoleColor,``0[])")]
[assembly: SuppressMessage("Performance", "SYSLIB1045:In „GeneratedRegexAttribute“ konvertieren.", Justification = "<Ausstehend>", Scope = "member", Target = "~M:Server_Messenger.Logging.Logger.LogInformation``1(``0[])")]
[assembly: SuppressMessage("Performance", "SYSLIB1045:In „GeneratedRegexAttribute“ konvertieren.", Justification = "<Ausstehend>", Scope = "member", Target = "~M:Server_Messenger.PersonalDataDatabase.HandleNpgsqlException(Npgsql.NpgsqlException)~Server_Messenger.Enums.NpgsqlExceptionInfos")]
#endregion

#region Easier using Suppressions
[assembly: SuppressMessage("Style", "IDE0063:Einfache using-Anweisung verwenden", Justification = "<Ausstehend>", Scope = "member", Target = "~M:Server_Messenger.Security.EncryptAesDatabase``2(``0)~``1")]
[assembly: SuppressMessage("Style", "IDE0063:Einfache using-Anweisung verwenden", Justification = "<Ausstehend>", Scope = "member", Target = "~M:Server_Messenger.Security.EncryptAes(System.Net.WebSockets.WebSocket,System.Byte[])~System.Byte[]")]
#endregion

#region Char overloading Suppressions
[assembly: SuppressMessage("Performance", "CA1866:Char-Überladung verwenden", Justification = "<Ausstehend>", Scope = "member", Target = "~M:Server_Messenger.Logging.Logger.LogError``1(``0)")]
#endregion