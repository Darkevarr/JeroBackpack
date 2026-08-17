using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;
using System.Reflection;
using System.Threading;
using System.Linq;

namespace JeroBackpack;

[Injectable(TypePriority = OnLoadOrder.PostLoad)] // меняем на стандартный
public class JeroBackpack(
    ISptLogger<JeroBackpack> logger,
    TemplateTable templateTable,
    ModHelper modHelper
) : IOnLoad
{
    private const string BACKPACK_PARENT_ID = "5448e53e4bdc2d60728b4567";

    private ModConfig? _sizeMappingConfig;
    private ItemCustomConfig? _itemCustomConfig;
    private BlacklistConfig? _blacklistConfig;

    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // Первое логирование, чтобы проверить вызов метода
        logger.Info("[JERO] OnLoadAsync started!");

        var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        logger.Info($"[JERO] Mod path: {modPath}");
        var configFolderPath = Path.Combine(modPath, "config");
        logger.Info($"[JERO] Config folder: {configFolderPath}");

        // Загрузка конфигов
        try
        {
            _sizeMappingConfig = modHelper.GetJsonDataFromFile<ModConfig>(configFolderPath, "config.json");
            if (_sizeMappingConfig == null)
                _sizeMappingConfig = new ModConfig();
            logger.Info("[JERO] config.json loaded successfully.");
        }
        catch (Exception e)
        {
            logger.Error($"[JERO] ERROR loading config.json: {e.Message}");
            _sizeMappingConfig = new ModConfig();
        }

        try
        {
            _itemCustomConfig = modHelper.GetJsonDataFromFile<ItemCustomConfig>(configFolderPath, "item.json");
            if (_itemCustomConfig == null)
                _itemCustomConfig = new ItemCustomConfig();
            logger.Info("[JERO] item.json loaded successfully.");
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] ERROR loading item.json: {e.Message}");
            _itemCustomConfig = new ItemCustomConfig();
        }

        try
        {
            _blacklistConfig = modHelper.GetJsonDataFromFile<BlacklistConfig>(configFolderPath, "blacklist.json");
            if (_blacklistConfig == null)
                _blacklistConfig = new BlacklistConfig();
            logger.Info("[JERO] blacklist.json loaded successfully.");
        }
        catch (Exception e)
        {
            logger.Warning($"[JERO] ERROR loading blacklist.json: {e.Message}");
            _blacklistConfig = new BlacklistConfig();
        }

        if (_sizeMappingConfig?.SizeMappings == null || !_sizeMappingConfig.SizeMappings.TryGetValue(BACKPACK_PARENT_ID, out var sizeMappings))
        {
            logger.Warning($"[JERO] No size mappings found for Parent ID {BACKPACK_PARENT_ID} in config.json.");
            return;
        }

        logger.Info("[JERO] Starting backpack resizing (in-memory via TemplateTable)...");

        var itemsDb = templateTable.Items;
        int successCount = 0;
        int skippedCount = 0;

        foreach (var kvp in itemsDb)
        {
            var item = kvp.Value;
            var itemId = kvp.Key;

            if (item.Parent != BACKPACK_PARENT_ID)
                continue;

            if (_blacklistConfig?.Blacklist != null && _blacklistConfig.Blacklist.ContainsKey(itemId))
            {
                skippedCount++;
                continue;
            }

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

            int newH = oldH, newV = oldV;

            if (_itemCustomConfig?.Backpacks != null && _itemCustomConfig.Backpacks.TryGetValue(itemId, out var customSize))
            {
                newH = customSize.Horizontal;
                newV = customSize.Vertical;
            }
            else
            {
                string sizeKey = $"{oldH}x{oldV}";
                if (sizeMappings.TryGetValue(sizeKey, out var sizeMapping))
                {
                    newH = sizeMapping.NewHorizontal;
                    newV = sizeMapping.NewVertical;
                }
                else
                {
                    logger.Debug($"[JERO] No mapping for size {sizeKey} of backpack {itemId}");
                    continue;
                }
            }

            mainGrid.Properties.CellsH = newH;
            mainGrid.Properties.CellsV = newV;

            logger.Debug($"[JERO] Updated {itemId} ({item.Name}): {oldH}x{oldV} -> {newH}x{newV}");
            successCount++;
        }

        logger.Success($"[JERO] Completed! {successCount} backpacks modified, {skippedCount} skipped.");
    }
}