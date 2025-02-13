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

If needed, use a **custom method** like `ToCamelCase()` from the `StringExtensions` class  to enforce camelCase manually:  

```cs
yield return (nameof(User).ToCamelCase(), "Placeholder");
```
