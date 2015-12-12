# SuperDelete

###About
A Windows command-line tool that can be used to delete files and folders with very long paths (longer than MAX_PATH 260 characters). It supports paths as long as 32767 characters. 
It works by using extended-length paths and the Unicode versions of the WinApi functions for enumerating and deleting files. 

More info about the mechanism can be found in MSDN article [Naming Files, Paths, and Namespaces](https://msdn.microsoft.com/en-us/library/windows/desktop/aa365247(v=vs.85).aspx), in section "Maximum Path Length Limitation".

It's written in C#/NET and provides VS projects for building for .NET 3.5, 4.0, 4.5, 4.6

### Usage

It's fairly simple. Just open a command-line window and run the tool. It takes only one parameter, which can be a full file or folder path. 

```
SuperDelete.exe fullPathToFileOrFolder
```
