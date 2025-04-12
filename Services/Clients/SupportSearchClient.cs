using Azure.Search.Documents;

namespace SednaRag.Services.Clients
{
    public class SupportSearchClient
    {
        public SearchClient Client { get; }

        public SupportSearchClient(SearchClient client)
        {
            Client = client;
        }
    }
}
