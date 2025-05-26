# KnightBus.Core Changelog

# 7.1.0
* Add support for metadata in `IMessageAttachement`s

# 16.1.4
* (patch) Updated System.Text.Json version

# 16.1.3
* (patch) Updated System.Text.Json version

# 15.0.0
* Throw if etag differs when updating blob saga data

# 14.0.0
* Removed ConsoleWriter

## 8.4.0
* Added GetMapping for IMessage, to get IMessageMapper instance

## 8.3.0
* Added ISagaDuplicateDetected<> that can be used to handle the duplicated message before it is completed.  
    It can e.g. be used to re-schedule the message later on before it is deleted
