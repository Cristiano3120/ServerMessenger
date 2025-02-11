# Server Messenger

This is the server part of the messenger that I'm coding in my free time. If you are interested in contributing, just follow the steps below. If you have any questions, just refer to the "Rules and Other Important Infos" section.

## Steps:

1. **Fork this repository.**

2. **Download PostgreSQL** and create a database called `PersonalData`.

3. **Right-click the database** and click on `Restore`.

4. An explorer window will open. **Click on the file extension dropdown** in the bottom right corner and choose `All files`.

5. **Select the file** `ServerMessenger/ServerMessenger/Personal_Data_Database_Export.tar`

6. **Open the project** in your preferred code editor and open the developer console (for Visual Studio: right-click on `ClientMessenger.csproj` -> "Open in Terminal"). If you'd rather use the Windows Command Prompt search for "cmd" in your Windows search bar and open it.

7. **[Step for Windows Terminal only]** In the Windows CMD navigate to your project folder by typing `cd {path to your project}` (for example: `cd "C:\Users\Praktikum\source\repos\Cristiano3120\ServerMessenger\Server Messenger\"`). Be sure to use quotes("") if your path contains whitespaces like in this example.

8. **Run the following command** to mark the Settings folder as "unchanged" in the Git index, so it won’t be included in future commits. Make sure you provide the full path to the `Settings` folder (for example: `git update-index --assume-unchanged "C:\Users\Praktikum\source\repos\Cristiano3120\ServerMessenger\Server Messenger\Settings\"`), and use quotes("") again if the path contains whitespaces.

9. **Open the** `appsettings.json` file located in the `Settings` folder. Modify the settings according to your needs particularly the password for PostgreSQL that you used during installation. If necessary change the port (the port in the `settings.json` file is 5433, but it might be 5432 or something like that for your database). You can check the port by **opening** `pgAdmin4`, **right-clicking on** `PostgreSQL 17`, **selecting** `Properties`, and **going to** the `Connection` tab where you’ll find the correct port and other connection details.

10. The database should now be ready! **Now read the rules below**, then proceed with step 11.

11. **Go to** the [Client Messenger repository](https://github.com/Cristiano3120/ClientMessenger) now and follow the README there.

## Rules and Other Important Infos:

- **First**, if you have **ANY** questions, just hit me up on any of my linked socials (check my GitHub profile) or add me on Discord `Cristiano26`.

- **Second**, **ANY** contribution is appreciated. If you **add** a `comment`, **fix** a `bug`, **provide** `coding recommendations`, or whatever, I will be grateful for any help that I get.

### Rules:

- Please use the **editor config** file available in the repository. If you dislike the config, just ask me first and we can figure it out.

- Please **follow** the naming conventions and style guidelines: 

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
