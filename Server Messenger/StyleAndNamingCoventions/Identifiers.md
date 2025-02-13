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