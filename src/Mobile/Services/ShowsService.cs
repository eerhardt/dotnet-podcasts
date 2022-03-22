using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.NetConf2021.Maui.Models.Responses;
using MonkeyCache.FileStore;

namespace Microsoft.NetConf2021.Maui.Services;

public partial class ShowsService
{
    private readonly HttpClient httpClient;
    private readonly ListenLaterService listenLaterService;

    public ShowsService(ListenLaterService listenLaterService)
    {
        this.httpClient = new HttpClient() { BaseAddress = new Uri(Config.APIUrl) };
        this.listenLaterService = listenLaterService;
    }

    public async Task<IEnumerable<Category>> GetAllCategories()
    {
        var categoryResponse = await TryGetAsync("categories", JsonContext.Default.IEnumerableCategoryResponse);
        return categoryResponse?.Select(response => new Category(response));
    }

    public async Task<Show> GetShowByIdAsync(Guid id)
    {
        var showResponse = await TryGetAsync($"shows/{id}", JsonContext.Default.ShowResponse);

        return showResponse == null
            ? null
            : GetShow(showResponse);
    }

    public Task<IEnumerable<Show>> GetShowsAsync()
    {
        return SearchShowsAsync(string.Empty);
    }

    public async Task<IEnumerable<Show>> GetShowsByCategoryAsync(Guid idCategory)
    {
        var result = new List<Show>();
        var showsResponse = await TryGetShows($"shows?limit=10&categoryId={idCategory}");

        if (showsResponse == null)
            return result;
        else
        {
            foreach(var show in showsResponse)
            {
                result.Add(GetShow(show));
            }

            return result;
        }
    }

    public async Task<IEnumerable<Show>> SearchShowsAsync(Guid idCategory, string term)
    {
        var showsResponse = await TryGetShows($"shows?limit=10&categoryId={idCategory}&term={term}");

        return showsResponse?.Select(response => GetShow(response));
    }

    public async Task<IEnumerable<Show>> SearchShowsAsync(string term)
    {
        var showsResponse = await TryGetShows($"shows?limit=10&term={term}");

        return showsResponse?.Select(response => GetShow(response));
    }

    private Show GetShow(ShowResponse response)
    {
        return new Show(response, listenLaterService);
    }

    private Task<IEnumerable<ShowResponse>> TryGetShows(string path) =>
        TryGetAsync(path, JsonContext.Default.IEnumerableShowResponse);

    private async Task<T> TryGetAsync<T>(string path, JsonTypeInfo<T> jsonTypeInfo)
    {
        var json = string.Empty;

#if !MACCATALYST
        if (Connectivity.NetworkAccess == NetworkAccess.None)
            json = Barrel.Current.Get(path, JsonContext.Default.String);
#endif
        if (!Barrel.Current.IsExpired(path))
            json = Barrel.Current.Get(path, JsonContext.Default.String);

        T responseData = default;
        try
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                var response = await httpClient.GetAsync(path);
                if (response.IsSuccessStatusCode)
                {
                    responseData = await response.Content.ReadFromJsonAsync<T>(jsonTypeInfo);
                }
            }
            else
            {
                responseData = JsonSerializer.Deserialize<T>(json, jsonTypeInfo);
            }

            if (responseData != null)
                Barrel.Current.Add(path, responseData, TimeSpan.FromMinutes(10), jsonTypeInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }

        return responseData;
    }

    [JsonSerializable(typeof(IEnumerable<CategoryResponse>))]
    [JsonSerializable(typeof(IEnumerable<ShowResponse>))]
    private partial class JsonContext : JsonSerializerContext
    {
    }
}
