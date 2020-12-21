# Sir.Cmd

Sir.Cmd is a console application for interacting with Resin databases and indices. It runs built-in commands as well as `ICommand` commands 
that you may write yourself.

## Usage

`cd local_path_to_resin_repo`

### sir.bat truncate --collection [collectionName]

Removes a collection.

### sir.bat truncate-index --collection [collectionName]

Removes the index of a collection.

### sir.bat optimize --collection [collectionName] --skip --take --batchSize

Creates and optimized index out of all existing segments to make querying faster.

### sir.bat slice --sourceFileName [sourceFileName] --resultFileName [resultFileName] --length [bytes_to_read]

Help function for investigating big files.

### sir.bat your_custom_task --your_args [your_args]

Implement `Sir.ICommand` from the Sir.Core.dll. The name of your command is also how you reference it through this command-line tool. 
The command name is the class name minus the word "Command", lowercased. 
Drop your DLL file into Sir's bin folder and run it with `sir.bat`