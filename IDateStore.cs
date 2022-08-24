namespace ApiClient;

public interface IDataStore
{
    /// <summary>Asynchronously stores the given value for the given key (replacing any existing value).</summary>
    /// <typeparam name="T">The type to store in the data store.</typeparam>
    /// <param name="key">The key.</param>
    /// <param name="value">The value to store.</param>
    Task StoreAsync<T>(string key, T value);

    /// <summary>
    /// Asynchronously deletes the given key. The type is provided here as well because the "real" saved key should
    /// contain type information as well, so the data store will be able to store the same key for different types.
    /// </summary>
    /// <typeparam name="T">The type to delete from the data store.</typeparam>
    /// <param name="key">The key to delete.</param>
    Task DeleteAsync<T>(string key);

    /// <summary>Asynchronously returns the stored value for the given key or <c>null</c> if not found.</summary>
    /// <typeparam name="T">The type to retrieve from the data store.</typeparam>
    /// <param name="key">The key to retrieve its value.</param>
    /// <returns>The stored object.</returns>
    Task<T> GetAsync<T>(string key);

    /// <summary>Asynchronously clears all values in the data store.</summary>
    Task ClearAsync();
}