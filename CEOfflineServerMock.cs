using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CEOfflineServerMock
{
    private static CEOfflineServerMock _instance;
    public static CEOfflineServerMock Instance
    {
        get
        {
            if (_instance == null) _instance = new CEOfflineServerMock();
            return _instance;
        }
    }

    private string SavePath => Application.persistentDataPath + "/offline_save.json";
    public OfflineSaveData CurrentSave;

    public CEOfflineServerMock()
    {
        LoadData();
    }

    public void LoadData()
    {
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                CurrentSave = JsonUtility.FromJson<OfflineSaveData>(json);
                Debug.Log("[OfflineServer] Loaded save data from " + SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[OfflineServer] Failed to load save data: " + e.Message);
                CreateDefaultSave();
            }
        }
        else
        {
            CreateDefaultSave();
        }
    }

    public void SaveData()
    {
        try
        {
            string json = JsonUtility.ToJson(CurrentSave, true);
            File.WriteAllText(SavePath, json);
            Debug.Log("[OfflineServer] Saved data to " + SavePath);
        }
        catch (Exception e)
        {
            Debug.LogError("[OfflineServer] Failed to save data: " + e.Message);
        }
    }

    private void CreateDefaultSave()
    {
        Debug.Log("[OfflineServer] Creating new default sandbox profile.");
        CurrentSave = new OfflineSaveData
        {
            PlayerLevel = 48,
            Gold = 23000,
            Gems = 110,
            // 기본 픽시(레아나, 제니 등) 및 기본 슈트 언락 리스트 설정
            UnlockedPixies = new List<int> { 1, 2 }, // Pixie Template IDs
            UnlockedSuits = new List<int> { 11010, 11020 }, // Suit Template IDs
            ClearedMissions = new List<int> { 1, 2, 3 } // 클리어 완료한 미션 ID들
        };
        SaveData();
    }

    /// <summary>
    /// 들어온 패킷에 해당하는 가상 응답 객체를 리턴합니다.
    /// </summary>
    public CEPacketResponse HandlePacket(CEPacket packet)
    {
        ePacketType packetType = packet.PacketType;
        Debug.Log("[OfflineServer] HandlePacket : " + packetType);

        switch (packetType)
        {
            case ePacketType.Version:
                return new CEPacketResponseVersion { status_code = "success" };

            case ePacketType.Login:
                return GetLoginResponse();

            case ePacketType.GXS:
                return new CEPacketResponseGXS { status_code = "success", seed = "mock_seed" };

            case ePacketType.MailBoxInfo:
                return new CEPacketResponseMailBoxInfo { status_code = "success", mailbox = new Dictionary<long, CEPacketElementMailBox>() };

            case ePacketType.FriendList:
                return new CEPacketResponseFriendList { status_code = "success", friend = new Dictionary<long, CEPacketElementFriendList>() };

            case ePacketType.DailyAchievement:
                return new CEPacketResponseDailyAchievement { status_code = "success", daily_achievement = new Dictionary<long, CEPacketElementConditionDaily>() };

            case ePacketType.MissionStart:
                return HandleMissionStart(packet);

            case ePacketType.MissionEnd:
                return HandleMissionEnd(packet);
        }

        return null; // 정의되지 않은 패킷은 null을 리턴하여 원래 망 통신(또는 타임아웃)을 태우거나 우회 대기하도록 함
    }

    private CEPacketResponseLogin GetLoginResponse()
    {
        var response = new CEPacketResponseLogin
        {
            status_code = "success",
            pid = 12345L,
            auth_number = 12345,
            server_time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),

            stat = new CEPacketElementStat
            {
                name = "PreservedCommander",
                level = CurrentSave.PlayerLevel,
                exp = 0,
                credit = CurrentSave.Gold,
                gem = CurrentSave.Gems,
                ap = 120,
                max_ap = 120,
                bp = 10,
                max_bp = 10,
                player_pixie_id = 1L // 로비 전면에 등장할 가상 픽시 인스턴스 ID (1L = 레아나)
            },

            inventory = new CEPacketElementInventory
            {
                suit = CurrentSave.UnlockedSuits.Count,
                max_suit = 100,
                parts = 0,
                max_parts = 100
            },

            // 픽시 정보 매핑
            pixie = GetMockPixies(),
            // 슈트 정보 매핑
            suit = GetMockSuits(),
            // 편성 팀 정보 매핑
            mission_team = GetMockTeam(),
            // 스테이지 클리어 정보 매핑
            mission = GetMockMissions()
        };

        return response;
    }

    private Dictionary<long, CEPacketElementPixie> GetMockPixies()
    {
        var dict = new Dictionary<long, CEPacketElementPixie>();
        
        // 1번 픽시(레아나) - 1001L 인스턴스 슈트 탑승
        dict.Add(1L, new CEPacketElementPixie {
            pixie_id = 1,
            level = 45,
            likability = 200,
            player_suit_id = 1001L,
            wakeup = 1
        });
        
        // 2번 픽시(제니) - 1002L 인스턴스 슈트 탑승
        dict.Add(2L, new CEPacketElementPixie {
            pixie_id = 2,
            level = 45,
            likability = 200,
            player_suit_id = 1002L,
            wakeup = 1
        });

        return dict;
    }

    private Dictionary<long, CEPacketElementSuit> GetMockSuits()
    {
        var dict = new Dictionary<long, CEPacketElementSuit>();

        // 1001L 슈트 (레아나의 탑승 기체)
        dict.Add(1001L, new CEPacketElementSuit {
            suit_id = 11010,
            level = 50,
            player_pixie_id = 1L
        });

        // 1002L 슈트 (제니의 탑승 기체)
        dict.Add(1002L, new CEPacketElementSuit {
            suit_id = 11020,
            level = 50,
            player_pixie_id = 2L
        });

        return dict;
    }

    private CEPacketElementTeam GetMockTeam()
    {
        return new CEPacketElementTeam
        {
            slot_count = 5,
            player_pixie_id_1 = 1L, // 1번 슬롯: 레아나
            player_pixie_id_2 = 2L, // 2번 슬롯: 제니
            player_pixie_id_3 = 9223372036854775807L, // long.MaxValue (빈 슬롯)
            player_pixie_id_4 = 9223372036854775807L,
            player_pixie_id_5 = 9223372036854775807L
        };
    }

    private Dictionary<long, CEPacketElementMission> GetMockMissions()
    {
        var dict = new Dictionary<long, CEPacketElementMission>();
        var starsMap = new Dictionary<int, int>();

        foreach (var missionId in CurrentSave.ClearedMissions)
        {
            starsMap[missionId] = 3; // 클리어 완료한 미션들은 별 3개 처리
        }

        dict.Add(1L, new CEPacketElementMission {
            chapter_id = 1,
            star = starsMap.Count * 3,
            stars = starsMap
        });

        return dict;
    }

    private CEPacketResponseMissionStart HandleMissionStart(CEPacket packet)
    {
        // 미션 시작 패킷을 분석하여 스테이지 진입 허용
        var startPacket = packet as CEPacketMissionStart;
        var response = new CEPacketResponseMissionStart
        {
            status_code = "success",
            chapter_id = 1,
            mission_id = (startPacket != null) ? startPacket.mission_id : 1
        };
        return response;
    }

    private CEPacketResponseMissionEnd HandleMissionEnd(CEPacket packet)
    {
        // 전투 성공 패킷이 수신되면 로컬 JSON 세이브 데이터를 업데이트하고 저장
        var endPacket = packet as CEPacketMissionEnd;
        if (endPacket != null)
        {
            int finishedMissionId = endPacket.mission_id;
            if (!CurrentSave.ClearedMissions.Contains(finishedMissionId))
            {
                CurrentSave.ClearedMissions.Add(finishedMissionId);
            }
            CurrentSave.Gold += 1000; // 보상 골드 임의 추가
            CurrentSave.Gems += 10;  // 보상 젬 임의 추가
            SaveData();
        }

        var response = new CEPacketResponseMissionEnd
        {
            status_code = "success",
            stat = new CEPacketElementStat
            {
                level = CurrentSave.PlayerLevel,
                credit = CurrentSave.Gold,
                gem = CurrentSave.Gems
            },
            mission = GetMockMissions()
        };
        return response;
    }
}

[Serializable]
public class OfflineSaveData
{
    public int PlayerLevel;
    public long Gold;
    public int Gems;
    public List<int> UnlockedSuits;
    public List<int> UnlockedPixies;
    public List<int> ClearedMissions;
}