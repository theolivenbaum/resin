# Sir.DbUtil

Sir.DbUtil is a console application for interacting with Resin databases and indices. It runs built-in command as well as `ICommand` commands 
that you may write yourself.

## Usage

`cd local_path_to_resin_repo`

### dbutil.bat truncate --collection [collectionName]

Removes a collection.

### dbutil.bat truncate-index --collection [collectionName]

Removes the index of a collection.

### dbutil.bat optimize --collection [collectionName] --skip --take --batchSize

Creates one index segment out of all existing segments and makes querying faster.

### dbutil.bat slice --sourceFileName [sourceFileName] --resultFileName [resultFileName] --length [bytes_to_read]

Help function for investigating big files.

### dbutil.bat your_custom_task --your_args [your_args]

Implement `Sir.ICommand` from the Sir.Core.dll. The name of your command and how you reference it will be the class name minus the word "Command, lowercased". 
Drop your DLL file into DbUtil's bin folder to run it.