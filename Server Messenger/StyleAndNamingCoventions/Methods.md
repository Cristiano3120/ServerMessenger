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