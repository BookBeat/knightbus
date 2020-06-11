using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace KnightBus.Azure.Storage.Sagas
{
    public class SagaTableData : TableEntity
    {
        public string Json { get; set; }
        public DateTime? Expiration { get; set; }
    }
}