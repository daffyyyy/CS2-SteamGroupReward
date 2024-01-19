using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using System.Xml;

namespace CS2_SteamGroupReward;
[MinimumApiVersion(142)]

public class SteamGroupRewardConfig : BasePluginConfig
{
	[JsonPropertyName("Group_ID")] public int Group_ID { get; set; } = 0;
	[JsonPropertyName("Reward_Spawn_HP")] public int Reward_Spawn_HP { get; set; } = 105;
	[JsonPropertyName("Reward_Spawn_Armor")] public int Reward_Spawn_Armor { get; set; } = 100;
	[JsonPropertyName("Reward_Spawn_Money")] public int Reward_Spawn_Money { get; set; } = 200;
	[JsonPropertyName("Reward_Kill_HP")] public int Reward_Kill_HP { get; set; } = 5;
	[JsonPropertyName("Reward_Kill_Money")] public int Reward_Kill_Money { get; set; } = 100;
}

public class CS2_SteamGroupReward : BasePlugin, IPluginConfig<SteamGroupRewardConfig>
{
	public SteamGroupRewardConfig Config { get; set; } = new SteamGroupRewardConfig();
	public string SteamGroupInfoUrl = "http://steamcommunity.com/gid/10358279GROUP_ID/memberslistxml/?xml=1";
	HashSet<SteamID> MembersCache = new HashSet<SteamID>();

	public override string ModuleName => "CS2-SteamGroupReward";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "daffyy";
	public override string ModuleDescription => "A plugin that grants privileges for being a member of a steam group";

	public override void Load(bool hotReload)
	{
		RegisterListener<Listeners.OnMapStart>(OnMapStartHandler);

		if (hotReload)
		{
			OnMapStartHandler(string.Empty);
		}
	}

	public void OnConfigParsed(SteamGroupRewardConfig config)
	{
		if (config.Group_ID == 0)
		{
			throw new Exception($"Invalid value has been set for config value `Group_ID`");
		}

		SteamGroupInfoUrl = SteamGroupInfoUrl.Replace("GROUP_ID", (1429521408 + config.Group_ID).ToString());
		Config = config;
	}

	[GameEventHandler]
	public HookResult EventPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Userid;

		if (player == null || !player.IsValid || player.IsBot || player.AuthorizedSteamID == null) return HookResult.Continue;

		if (player.PlayerPawn.Value != null && MembersCache.Contains(player.AuthorizedSteamID))
		{
			if (player.InGameMoneyServices != null)
				player.InGameMoneyServices.Account += Config.Reward_Spawn_Money;

			AddTimer(0.1f, () =>
			{
				if (player.PlayerPawn.Value.ItemServices != null && Config.Reward_Spawn_Armor == 100)
					new CCSPlayer_ItemServices(player.PlayerPawn.Value.ItemServices.Handle).HasHelmet = true;

				player.PlayerPawn.Value.ArmorValue = Config.Reward_Spawn_Armor;
				player.PawnArmor = Config.Reward_Spawn_Armor;

				if (Config.Reward_Spawn_HP > 100)
				{
					player.PlayerPawn.Value.MaxHealth = Config.Reward_Spawn_HP;
					player.MaxHealth = Config.Reward_Spawn_HP;
				}

				player.PlayerPawn.Value.Health = Config.Reward_Spawn_HP;
				player.Health = Config.Reward_Spawn_HP;

				Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
			});
		}

		return HookResult.Continue;
	}

	[GameEventHandler]
	public HookResult EventPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
	{
		CCSPlayerController? player = @event.Attacker;

		if (player == null || !player.IsValid || @event.Attacker == @event.Userid || player.IsBot || player.AuthorizedSteamID == null) return HookResult.Continue;


		if (player.PlayerPawn.Value != null && MembersCache.Contains(player.AuthorizedSteamID))
		{
			if (player.InGameMoneyServices != null)
				player.InGameMoneyServices.Account += Config.Reward_Spawn_Money;

			if (Config.Reward_Kill_HP > 100)
				player.PlayerPawn.Value.MaxHealth = Config.Reward_Kill_HP;
			player.PlayerPawn.Value.Health += Config.Reward_Kill_HP;


			Utilities.SetStateChanged(player.PlayerPawn.Value, "CBaseEntity", "m_iHealth");
		}

		return HookResult.Continue;
	}

	private void OnMapStartHandler(string mapName)
	{
		AddTimer(1.0f, () => _ = FetchMembersFromGroupAsync());
	}

	private async Task FetchMembersFromGroupAsync()
	{
		try
		{
			using (HttpClient client = new HttpClient())
			{
				HttpResponseMessage response = await client.GetAsync(SteamGroupInfoUrl);

				if (response.IsSuccessStatusCode)
				{
					string groupInfo = await response.Content.ReadAsStringAsync();
					ParseMembers(groupInfo);
				}
				else
				{
					Logger.LogError("Unable to fetch group info!");
				}
			}
		}
		catch (Exception)
		{
			Logger.LogWarning("Unknown error with parsing group info");
		}
	}

	private void ParseMembers(string groupInfo)
	{
		try
		{
			XmlDocument xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(groupInfo);

			XmlNodeList? SteamIdNodes = xmlDoc.SelectNodes("//members/steamID64");

			if (SteamIdNodes != null)
			{
				MembersCache.Clear();
				foreach (XmlNode node in SteamIdNodes)
				{
					string SteamId64 = node.InnerText;
					if (!string.IsNullOrEmpty(SteamId64) && SteamID.TryParse(SteamId64, out var steamId) && steamId != null)
					{
						if (!MembersCache.Contains(steamId))
							MembersCache.Add(steamId);
					}
				}
			}
		}
		catch (Exception)
		{
			Logger.LogWarning("Unable to parse members from steam group!");
		}
	}
}

