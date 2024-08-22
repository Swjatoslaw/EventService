using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class EventService : MonoBehaviour
{
    #region nested types

    [Serializable]
    private class Event
    {
        public string type;
        public string data;

        public Event(string _Type, string _Data)
        {
            type = _Type;
            data = _Data;
        }
    }

    [Serializable]
    private class EventList
    {
        public List<Event> events;

        public EventList(List<Event> _Events)
        {
            events = _Events;
        }
    }

    #endregion
    
    #region constants

    private const string SERVER_URL = "https://127.0.0.1:2043";
    private const float COOLDOWN_BEFORE_SEND = 2;
    private const string STORED_EVENTS_KEY = "StoredEvents";
    private const int MAX_SEND_ATTEMPTS = 5;
    
    #endregion
    
    #region attributes
    
    private static readonly Dictionary<string, Event> eventRegistry = new ();
    
    private HttpClient httpClient = new ();
    private bool isCooldownActive;

    #endregion
    
    #region engine methods
    
    private async void Start()
    {
        await LoadStoredEventsAsync();
    }
    
    private void OnApplicationFocus(bool _Focus)
    {
        if(!_Focus)
            SaveEvents();
    }

    private void OnApplicationQuit()
    {
        SaveEvents();
    }

    #endregion
    
    #region public methods

    public void TrackEvent(string _Type, string _Data)
    {
        Event newEvent = new Event(_Type, _Data);
        eventRegistry.Add(Guid.NewGuid().ToString(), newEvent);

        if (!isCooldownActive)
            CooldownAndSendEventsAsync().Forget();
    }
    
    #endregion

    #region service methods

    private async UniTask CooldownAndSendEventsAsync()
    {
        isCooldownActive = true;
        int attemptsLeft = MAX_SEND_ATTEMPTS;
        
        while (eventRegistry.Count > 0 && attemptsLeft > 0)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(COOLDOWN_BEFORE_SEND));
            await SendEventsToServerAsync();
            attemptsLeft--;
        }
        
        isCooldownActive = false;
    }

    private async UniTask SendEventsToServerAsync()
    {
        EventList eventList = new EventList(eventRegistry.Values.ToList());
        string jsonData = JsonUtility.ToJson(eventList);
        List<string> processingEvents = eventRegistry.Keys.ToList();

        try
        {
            using UnityWebRequest request = new UnityWebRequest(SERVER_URL, UnityWebRequest.kHttpVerbPOST);
            
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            var response = await request.SendWebRequest();

            if (response.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Events sent successfully: {jsonData}");
                
                foreach (string eventKey in processingEvents)
                    eventRegistry.Remove(eventKey);
                
                PlayerPrefs.DeleteKey(STORED_EVENTS_KEY);
            }
            else
            {
                Debug.LogError($"Failed to send events: {response.error}");
                SaveEvents();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while sending events: {ex.Message}");
            SaveEvents();
        }
    }

    private void SaveEvents()
    {
        EventList eventList = new EventList(eventRegistry.Values.ToList());
        string jsonData = JsonUtility.ToJson(eventList);
        
        PlayerPrefs.SetString(STORED_EVENTS_KEY, jsonData);
        PlayerPrefs.Save();
    }

    private async UniTask LoadStoredEventsAsync()
    {
        if (PlayerPrefs.HasKey(STORED_EVENTS_KEY))
        {
            string jsonData = PlayerPrefs.GetString(STORED_EVENTS_KEY);
            EventList eventList = JsonUtility.FromJson<EventList>(jsonData);
            
            eventRegistry.Clear();
            
            foreach (Event item in eventList.events)
                eventRegistry.Add(Guid.NewGuid().ToString(), item);
            
            if (eventRegistry.Count > 0)
                await CooldownAndSendEventsAsync();
        }
    }
    
    #endregion
}
