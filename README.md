# Server Messenger

This is the server part of the messenger that I'm coding in my free time. If you are interested in contributing, just follow the steps below. If you have any questions, just refer to the "Rules and Other Important Infos" section.

## Steps:

1. **Fork this repository.**

2. **Download PostgreSQL** and create a database called "PersonalData".

3. **Right-click the database** and click on "Restore".

4. An explorer window will open. **Click on the file extension dropdown** in the bottom right corner and choose "All files".

5. **Select the file** `ServerMessenger/ServerMessenger/Personal_Data_Database_Export.tar`

6. Read the rules below, then proceed to step 7.

7. The database should now be ready. Go to the [Client Messenger repository](https://github.com/Cristiano3120/ClientMessenger) now and follow the README there.

## Rules and Other Important Infos:

- **First**, if you have **ANY** questions, just hit me up on any of my linked socials (check my GitHub profile) or add me on Discord (Cristiano26).

- **Second**, **ANY** contribution is appreciated. If you add a comment, fix a bug, provide coding recommendations, or whatever, I will be grateful for any help that I get.

### Rules:

- Please use the editor config file available in the repository. If you dislike the config, just ask me first and we can figure it out.

- Please follow the naming conventions and style guidelines: 

```cs
public class ShowCasingClass
{
    // For private fields (If you need the field to be public, make it a property)
    private readonly int _thisIsAField;

    // For public properties and constants
    public int ThisIsAProperty { get; set; }

    // For methods
    public void ThisIsAMethod<T>(int param)
    {
        // For local variables (ONLY USE 'var' WHEN THE TYPE IS APPARENT!)
        var thisIsALocalVar = "";

        // For instantiating
        ShowCasingClass show = new();
    }
}
