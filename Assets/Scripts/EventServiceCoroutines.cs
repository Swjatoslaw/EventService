using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

// For WebGL, it's better to use coroutines and UnityWebRequest because they are more stable in WebGL's
// single-threaded environment, avoiding issues that can arise with Task.Delay and async/await.
// UnityWebRequest is optimized and fully supported in WebGL, ensuring better compatibility
// compared to other network APIs like HttpClient.

public class EventServiceCoroutines : MonoBehaviour
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
    
    private bool isCooldownActive;

    #endregion
    
    #region engine methods

    private void Start()
    {
        LoadStoredEvents();
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
            StartCoroutine(CooldownAndSendEvents());
    }

    #endregion

    #region service methods

    private IEnumerator CooldownAndSendEvents()
    {
        isCooldownActive = true;
        int attemptsLeft = MAX_SEND_ATTEMPTS;
        
        while (eventRegistry.Count > 0 && attemptsLeft > 0)
        {
            yield return new WaitForSeconds(COOLDOWN_BEFORE_SEND);
            yield return SendEventsToServer();
            attemptsLeft--;
        }
        
        isCooldownActive = false;
    }

    private IEnumerator SendEventsToServer()
    {
        EventList eventList = new EventList(eventRegistry.Values.ToList());
        string jsonData = JsonUtility.ToJson(eventList);
        List<string> processingEvents = eventRegistry.Keys.ToList();
        
        using (UnityWebRequest request = new UnityWebRequest(SERVER_URL, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Events sent successfully: {jsonData}");

                foreach (string eventKey in processingEvents)
                    eventRegistry.Remove(eventKey);

                PlayerPrefs.DeleteKey(STORED_EVENTS_KEY);
            }
            else
            {
                Debug.LogError("Failed to send events: " + request.error);
                SaveEvents();
            }
        }
    }
    
    private void SaveEvents()
    {
        EventList eventList = new EventList(eventRegistry.Values.ToList());
        string jsonData = JsonUtility.ToJson(eventList);
        
        PlayerPrefs.SetString(STORED_EVENTS_KEY, jsonData);
        PlayerPrefs.Save();
    }

    private void LoadStoredEvents()
    {
        if (PlayerPrefs.HasKey(STORED_EVENTS_KEY))
        {
            string jsonData = PlayerPrefs.GetString(STORED_EVENTS_KEY);
            EventList eventList = JsonUtility.FromJson<EventList>(jsonData);
            
            eventRegistry.Clear();
            
            foreach (Event item in eventList.events)
                eventRegistry.Add(Guid.NewGuid().ToString(), item);

            if (eventRegistry.Count > 0)
                StartCoroutine(CooldownAndSendEvents());
        }
    }

    #endregion
}
