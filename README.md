# Server Messenger

This is the server part of the messenger that I'm coding in my free time. If you are interested in contributing, just follow the steps below. If you have any questions, just refer to the "Rules and Other Important Infos" section.

## Steps:

1. **Fork this repository.**

2. **Download PostgreSQL**.

3. **Go to** your `windows search bar` and type in `cmd`. **Open it** and **type in** `dotnet tool install --global dotnet-ef`. Now just **press enter** and **wait** for it to finnish.

4. **Type in** `cd {path to your project}` (for example: `cd "C:\Users\Praktikum\source\repos\Cristiano3120\ServerMessenger\Server Messenger\"`). Be sure to use quotes("") if your path contains whitespaces like in this example.

5. **Run the followings commands** `dotnet ef migrations add InitialCreate` then `dotnet ef database update` and at last to mark the Settings folder as "unchanged" in the Git index, so it won’t be included in future commits. **Type in** (and **make sure** you provide the full path to the `Settings` folder for example: `git update-index --assume-unchanged "C:\Users\Praktikum\source\repos\Cristiano3120\ServerMessenger\Server Messenger\Settings\"`), and use quotes("") if the path contains whitespaces.

6. **Open the** `appsettings.json` file located in the `Settings` folder. Modify the settings according to your needs particularly the password for PostgreSQL that you used during installation. If necessary change the port (the port in the `settings.json` file is **5433**, but it might be **5432** or something like that for your database). You can check the port by **opening** `pgAdmin4`, **right-clicking on** `PostgreSQL 17`, **selecting** `Properties`, and **going to** the `Connection` tab where you’ll find the correct port and other connection details.

7. The database should now be ready! **Now read the rules below**, then proceed with step 8.

8. **Go to** the [Client Messenger repository](https://github.com/Cristiano3120/ClientMessenger) now and follow the README there.

## Rules and Other Important Infos:

- **First**, if you have **ANY** questions, just hit me up on any of my linked socials (check my GitHub profile) or add me on Discord `Cristiano26`.

- **Second**, **ANY** contribution is appreciated. If you **add** a `comment`, **fix** a `bug`, **provide** `coding recommendations`, or whatever, I will be grateful for any help that I get.

### Rules:

- Please use the **editor config** file available in the repository. If you dislike the config, just ask me first and we can figure it out.

- Please **follow** the naming conventions and style guidelines: 

# **Naming Conventions for Fields, Properties, Constants**

This part defines the rules for naming fields, properties, and constants.

## 🔹 Private Fields

➡ **Private fields** should use an underscore prefix (`_`), making them easily distinguishable from other variables.
➡ **Use camelCase** for private fields.

**Example:**

```csharp
private readonly int _thisIsAField;
```

## 🔹 Public Properties

➡ **Public properties** should use **PascalCase**.
➡ **Auto-implemented properties** are recommended where possible.

**Example:**

```csharp
public int ThisIsAProperty { get; set; }
```

## 🔹 Public Readonly Fields

➡ Always use **PascalCase** for public readonly fields.

**Example:**

```csharp
public readonly string ThisIsAReadonlyField;
```

## 🔹 Constants

➡ **Constants** should be written in **UPPERCASE** to differentiate them from other variables, especially for values like magic numbers.

**Example:**

```csharp
public const byte ThisIsAConst = 3;
```

## 🔹 Special Case for Windows API Values

➡ Use **SHOUTING_SNAKE_CASE** for **Windows API constants** or any value that is a part of an external standard.

**Example:**

```csharp
private const int WM_SYSCOMMAND = 0x112;
```

# **Naming Conventions for Methods, Variables, and Avoiding Magic Numbers**

This part defines rules for naming methods, variables, and avoiding magic numbers.

## 🔹 Avoid Magic Numbers

➡ **Magic numbers** should always be avoided. These are hardcoded values that have no clear meaning or explanation in the code.

Instead, use **parameters**, **constants** **etc.** for these numbers. In the example below, the magic number `5` is avoided by passing it as a parameter:

**Example:**

```csharp
static Logger()
{
    AllocConsole();
    _pathToLogFile = MaintainLoggingSystem(maxAmmountLoggingFiles: 5); // Using a parameter instead of a magic number
}

private static string MaintainLoggingSystem(int maxAmountLoggingFiles)
{
    string pathToLoggingDic = Client.GetDynamicPath(@"Logging/");
    string[] files = Directory.GetFiles(pathToLoggingDic, "*.md");

    if (files.Length >= maxAmountLoggingFiles)
    {
        files = files.OrderBy(File.GetCreationTime).ToArray();
        int filesToRemove = files.Length - maxAmountLoggingFiles + 1;

        for (int i = 0; i < filesToRemove; i++)
        {
            File.Delete(files[i]);
        }
    }

    var timestamp = DateTime.Now.ToString("dd-MM-yyyy/HH-mm-ss");
    var pathToNewFile = Client.GetDynamicPath($"Logging/{timestamp}.md");
    File.Create(pathToNewFile).Close();
    return pathToNewFile;
}
```

## 🔹 Method and Variable Naming Convention

➡ **Local Variables** should be written in **camelCase**

- **Use `var` only when the type is clear from the context**.

**Example for local variables and correct usage of `var`:**

```csharp
public void ThisIsAMethod<T>(int param)
{
    var thisIsALocalVar = "";  // Right: 'var' used because the type is obvious

    // Wrong: 'var' is used when the type is not obvious
    var thisIsAReturnVar = DoSomething();

    // Right: Use explicit types when the return type is not apparent
    byte[] thisIsAReturnVar2 = DoSomething();
    var streamWriter = new StreamWriter("");
    var image = Image.FromFile("");
    var byteArr = ConvertToByteArr();
}
```

## 🔹 Async Methods Naming Convention

- Always use the prefix `Async` for asynchronous methods.

**Example of a method with the async prefix:**

```csharp
public async Task GetInfosFromTheDatabaseAsync()
{
    await DoSomethingAsync();
} 
```

# **JsonStylingConventions**

This part defines rules for working with JSON and ensures that all JSON properties follow the **camelCase** convention.

## 🔹 General Rule  

➡ **All JSON properties must be in camelCase**  

## 🔹 Use Global `JsonSerializerOptions`  

The **server** class has a **static `JsonSerializerOptions` instance** that should be used for all JSON operations:  

```cs
public static JsonSerializerOptions JsonSerializerOptions { get; private set; } = new();
```

## 🔹 Enforce camelCase with `JsonNamingPolicy.CamelCase`  

In the `Server.Start()` method, we ensure that **all properties are written in camelCase** by default:  

```cs
JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
```

- **Nested objects** like a nested class or a property etc. still require additional handling  

**Example JSON:**  

```json
{
  "opCode": 1,
  "npgsqlExceptionInfos": { "exception": 0, "columnName": "" },
  "user": { "username": "Cris", "email": "cris@cris.com" }
}
```

## 🔹 Naming Conventions in JSON Payloads  

- The **JSON payload** should always be named `payload`  
- If a variable represents an **enum** or **class** etc. use its camelCase equivalent  

**Example:**  

```cs
var payload = new
{
    opCode = OpCode.AnswerToLogin,   // Enum OpCode
    npgsqlExceptionInfos,            // Class NpgsqlExceptionInfos
    user,                            // Class User
};
```

## 🔹 Using `[JsonPropertyName]` for Custom Naming  

If a property does not follow camelCase automatically use the **`[JsonPropertyName]` attribute**:  

This again is needed when you sent a class as an payload that has propertys (which are written in PascalCase)

**Example from the User class**

```cs
[JsonPropertyName("user")]
public string Username { get; set; }
```

**Example: The user class sent as Json**:

```json
{
  "username": "Cris",
    "hashTag": "#Cris",
    "email": "Cris@cris.com",
    "password": "",
    "biography": "Cris",
    "id": "1",
    "birthday": "01.01.2020",
    "profilePicture": "[Image]",
}
```

**If the JsonPropertyName attribute would not be used all the properties would be PascalCase**



## 🔹 Convert to camelCase Manually with `ToCamelCase()`  

If needed, use a **custom method** like `ToCamelCase()` from the `StringExtensions` class to enforce camelCase manually:  

```cs
yield return (nameof(User).ToCamelCase(), "Placeholder");
```
