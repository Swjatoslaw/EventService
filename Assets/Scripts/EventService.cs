using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

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
            _ = CooldownAndSendEventsAsync();
    }
    
    #endregion

    #region service methods

    private async Task CooldownAndSendEventsAsync()
    {
        isCooldownActive = true;
        int attemptsLeft = MAX_SEND_ATTEMPTS;
        
        while (eventRegistry.Count > 0 && attemptsLeft > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(COOLDOWN_BEFORE_SEND));
            await SendEventsToServerAsync();
            attemptsLeft--;
        }
        
        isCooldownActive = false;
    }

    private async Task SendEventsToServerAsync()
    {
        EventList eventList = new EventList(eventRegistry.Values.ToList());
        string jsonData = JsonUtility.ToJson(eventList);
        List<string> processingEvents = eventRegistry.Keys.ToList();

        try
        {
            StringContent content = new StringContent(jsonData, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync(SERVER_URL, content);

            if (response.IsSuccessStatusCode)
            {
                Debug.Log($"Events sent successfully: {jsonData}");
                
                foreach (string eventKey in processingEvents)
                    eventRegistry.Remove(eventKey);
                
                PlayerPrefs.DeleteKey(STORED_EVENTS_KEY);
            }
            else
            {
                Debug.LogError($"Failed to send events: {response.StatusCode}");
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

    private async Task LoadStoredEventsAsync()
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
