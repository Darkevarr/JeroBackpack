using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Common.Models.Logging;
using System.Reflection;
using System.Threading;
using System.Linq;   // <-- добавили

namespace JeroBackpack;

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public class JeroBackpack(
    ISptLogger<JeroBackpack> logger,
    ItemHelper itemHelper,
    ModHelper modHelper
) : IOnLoad
{
    private const string BACKPACK_PARENT_ID = "5448e53e4bdc2d60728b4567";
    
    private ModConfig? _sizeMappingConfig;
    private ItemCustomConfig? _itemCustomConfig;
    private BlacklistConfig? _blacklistConfig;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var configFolderPath = Path.Combine(modPath, "config");

        // Загрузка config.json
        try
        {
            _sizeMappingConfig = modHelper.GetJsonDataFromFile<ModConfig>(configFolderPath, "config.json");
            if (_sizeMappingConfig == null)
            {
                logger.Warning("[JERO] JeroBackpack: config.json not found or empty. Using default values.");
                _sizeMappingConfig = new ModConfig();
            }
        }
        catch (Exception e)
        {
            logger.Error($"[JERO] JeroBackpack: ERROR loading config.json. Details: {e.Message}");
            _sizeMappingConfig = new ModConfig();
        }

        // Загрузка item.json
        try
        {
            _itemCustomConfig = modHelper.GetJsonDataFromFile<ItemCustomConfig>(configFolderPath, "item.json");
            if (_itemCustomConfig == null)
            {
                logger.Info("[JERO] JeroBackpack: item.json not found. No specific customizations will be applied.");
                _itemCustomConfig = new ItemCustomConfig();
            }
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] JeroBackpack: ERROR loading item.json. Details: {e.Message}");
            _itemCustomConfig = new ItemCustomConfig();
        }

        // Загрузка blacklist.json
        try
        {
            _blacklistConfig = modHelper.GetJsonDataFromFile<BlacklistConfig>(configFolderPath, "blacklist.json");
            if (_blacklistConfig == null)
            {
                logger.Info("[JERO] JeroBackpack: blacklist.json not found. No backpacks will be blocked.");
                _blacklistConfig = new BlacklistConfig();
            }
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] JeroBackpack: ERROR loading blacklist.json. Details: {e.Message}");
            _blacklistConfig = new BlacklistConfig();
        }

        logger.Info("[JERO] JeroBackpack: Starting backpack resizing...");

        var itemsDb = itemHelper.GetItemsClone();
        int successCount = 0;
        int skippedCount = 0;

        if (_sizeMappingConfig?.SizeMappings == null || !_sizeMappingConfig.SizeMappings.TryGetValue(BACKPACK_PARENT_ID, out var sizeMappings))
        {
            logger.Warning($"[JERO] JeroBackpack: No size mappings found for Parent ID {BACKPACK_PARENT_ID} in config.json.");
            return;
        }

        foreach (var item in itemsDb)
        {
            if (item.Parent != BACKPACK_PARENT_ID)
                continue;

            if (_blacklistConfig?.Blacklist != null && _blacklistConfig.Blacklist.ContainsKey(item.Id))
            {
                skippedCount++;
                continue;
            }

            // Исправленный блок работы с сетками
            var grids = item.Properties?.Grids;
            if (grids == null || !grids.Any())
                continue;

            if (grids.Count() > 1)
            {
                skippedCount++;
                continue;
            }

            var mainGrid = grids.FirstOrDefault();
            if (mainGrid?.Properties == null)
                continue;

            int oldH = mainGrid.Properties.CellsH ?? 0;
            int oldV = mainGrid.Properties.CellsV ?? 0;

            if (oldH == 0 || oldV == 0)
                continue;

            if (_itemCustomConfig?.Backpacks != null && _itemCustomConfig.Backpacks.TryGetValue(item.Id, out var customSize))
            {
                mainGrid.Properties.CellsH = customSize.Horizontal;
                mainGrid.Properties.CellsV = customSize.Vertical;
                successCount++;
            }
            else
            {
                string sizeKey = $"{oldH}x{oldV}";
                if (sizeMappings.TryGetValue(sizeKey, out var sizeMapping))
                {
                    mainGrid.Properties.CellsH = sizeMapping.NewHorizontal;
                    mainGrid.Properties.CellsV = sizeMapping.NewVertical;
                    successCount++;
                }
                else
                {
                    logger.Debug($"[JERO] JeroBackpack: No mapping found for size {sizeKey} of backpack '{item.Name}' (ID: {item.Id}).");
                }
            }
        }

        logger.Success($"[JERO] JeroBackpack: Completed! {successCount} backpacks modified, {skippedCount} backpacks ignored (blacklist or multiple grids).");
        return;
    }
}