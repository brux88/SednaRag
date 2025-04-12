using Azure.Search.Documents;

namespace SednaRag.Services.Clients
{
    public class ErpActionSearchClient
    {
        public SearchClient Client { get; }

        public ErpActionSearchClient(SearchClient client)
        {
            Client = client;
        }
    }
}
