using AutoRetainerSellList.Domain.Repositories;

namespace AutoRetainerSellList.Infrastructure.Localization;

public class ChatMessageService
{
    private readonly IConfigurationRepository _configRepository;

    public ChatMessageService(IConfigurationRepository configRepository)
    {
        _configRepository = configRepository;
    }

    public async Task<string> GetItemListedMessageAsync(int price)
    {
        var language = await _configRepository.GetChatLanguageAsync();
        return language switch
        {
            "English" => $" listed for {price:N0} gil",
            "Japanese" => $" を {price:N0} ギルで出品しました",
            _ => $" を {price:N0} ギルで出品しました"
        };
    }

    public async Task<string> GetAlreadyListedMessageAsync(int currentListed, int quantity)
    {
        var language = await _configRepository.GetChatLanguageAsync();
        return language switch
        {
            "English" => $" is already listed {currentListed}/{quantity}",
            "Japanese" => $" はすでに {currentListed}/{quantity} 出品済みです",
            _ => $" はすでに {currentListed}/{quantity} 出品済みです"
        };
    }

    public async Task<string> GetItemNotFoundMessageAsync()
    {
        var language = await _configRepository.GetChatLanguageAsync();
        return language switch
        {
            "English" => " not found in retainer inventory",
            "Japanese" => " がリテイナー所持品に見つかりません",
            _ => " がリテイナー所持品に見つかりません"
        };
    }

    public async Task<string> GetPriceFailureMessageAsync()
    {
        var language = await _configRepository.GetChatLanguageAsync();
        return language switch
        {
            "English" => " failed to get price. Canceling listing.",
            "Japanese" => " の価格取得に失敗しました。出品をキャンセルします。",
            _ => " の価格取得に失敗しました。出品をキャンセルします。"
        };
    }

    public async Task<string> GetProcessingErrorMessageAsync()
    {
        var language = await _configRepository.GetChatLanguageAsync();
        return language switch
        {
            "English" => " an error occurred during processing.",
            "Japanese" => " の処理中にエラーが発生しました。",
            _ => " の処理中にエラーが発生しました。"
        };
    }
}
