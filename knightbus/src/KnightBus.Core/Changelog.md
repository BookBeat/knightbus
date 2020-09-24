# KnightBus.Core Changelog
## 8.3.0
* Added ISagaDuplicateDetected<> that can be used to handle the duplicated message before it is completed.  
    It can e.g. be used to re-schedule the message later on before it is deleted
