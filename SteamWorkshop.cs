using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;

/// <summary>
/// This class was created by Vitor Pêgas on 01/01/2017
/// Link to original script: https://answers.unity.com/questions/1282772/steamworksnet-and-steam-workshop.html
///  
///  Updates: Added thumbnail uploading and added a subscribed item update checker
///  Updated by: Savva Madar
/*   Usage: Call SaveToWorkshop with the correct arguments to upload a file and a thumbnail to the steam workshop.
            SaveToWorkshop(string fileName, string fileData, string workshopTitle, string workshopDescription, string[] tags, string imageLoc);
			
			string fileName = the file name it will be saved as. When someone downloads the item this is the file they will download. Extension optional, doesn't need to match
			local file name if uploading local file.
			string fileData = the file contents.
			string workshopTitle = the title that the item will appear under.
			string workshopDescription = the description of the item.
			string[] tags = an array of strings each representing a tag for the workshop item.
			string imageLoc = the ABSOLUTE, not relative, location of the image file you would like to use as the thumbnail for your item.
			
			Call GetSubscribedItems to fetch a list of items that current user is subscribed to.
			GetSubscribedItems();
			
			Call GetItemContent(int itemID) with the correct argument and only after calling GetSubscribedItems to download an item IF it doesn't exist or needs to be updated.
			int itemID = an integer representing the index location inside subscribedItemList denoting which item needs to be checked. 

*/
/// </summary>
public class SteamWorkshop : MonoBehaviour
{
    public static SteamWorkshop singleton;
	//whether or not current item has finished downloading
	public bool fetchedContent;
	//the contents of the downloaded item
	private string itemContent;
	//the name of the last file that was uploaded
    private string lastFileName;
	//the location of the thumbnail for this upload
	private string thisUploadsIamgeLoc = "";
	//list of all fetched subscribed items
	public List<PublishedFileId_t> subscribedItemList;
	
    void Awake() {
        singleton = this;
        subscribedItemList = new List<PublishedFileId_t>();
    }

    private CallResult<SubmitItemUpdateResult_t> ItemUpdateResult;
    private CallResult<RemoteStoragePublishFileResult_t> RemoteStoragePublishFileResult;
    private CallResult<RemoteStorageEnumerateUserSubscribedFilesResult_t> RemoteStorageEnumerateUserSubscribedFilesResult;
    private CallResult<RemoteStorageGetPublishedFileDetailsResult_t> RemoteStorageGetPublishedFileDetailsResult;
    private CallResult<RemoteStorageDownloadUGCResult_t> RemoteStorageDownloadUGCResult;
    private CallResult<RemoteStorageUnsubscribePublishedFileResult_t> RemoteStorageUnsubscribePublishedFileResult;


    private PublishedFileId_t publishedFileID;
    private UGCHandle_t UGCHandle;



    void OnEnable()
    {
        ItemUpdateResult = CallResult<SubmitItemUpdateResult_t>.Create(OnItemUpdateResult);
        RemoteStoragePublishFileResult = CallResult<RemoteStoragePublishFileResult_t>.Create(OnRemoteStoragePublishFileResult);
        RemoteStorageEnumerateUserSubscribedFilesResult = CallResult<RemoteStorageEnumerateUserSubscribedFilesResult_t>.Create(OnRemoteStorageEnumerateUserSubscribedFilesResult);
        RemoteStorageGetPublishedFileDetailsResult = CallResult<RemoteStorageGetPublishedFileDetailsResult_t>.Create(OnRemoteStorageGetPublishedFileDetailsResult);
        RemoteStorageDownloadUGCResult = CallResult<RemoteStorageDownloadUGCResult_t>.Create(OnRemoteStorageDownloadUGCResult);
        RemoteStorageUnsubscribePublishedFileResult = CallResult<RemoteStorageUnsubscribePublishedFileResult_t>.Create(OnRemoteStorageUnsubscribePublishedFileResult);
    }

    private void OnItemUpdateResult(SubmitItemUpdateResult_t pCallback, bool bIOFailure)
    {
        if (pCallback.m_eResult == EResult.k_EResultOK)
        {
            Debug.Log("The item is now uploaded with a thumbnail");
        }
        else
        {
            Debug.Log("The item is now uploaded with out a thumbnail");
        }
    }

    IEnumerator downloadFiles()
    {
        int dlItem = 0;
        while (dlItem < subscribedItemList.Count)
        {
            fetchedContent = false;
            GetItemContent(dlItem);
            while (fetchedContent == false)
            {
                yield return new WaitForEndOfFrame();
            }
            dlItem++;
        }
    }

    public void GetSubscribedItems()
    {
        SteamAPICall_t handle = SteamRemoteStorage.EnumerateUserSubscribedFiles(0);
        RemoteStorageEnumerateUserSubscribedFilesResult.Set(handle);
    }

    /// <summary>
    /// Gets the Item content (subscribed) to variable itemContent
    /// When done downloading, fetchedContent will be TRUE.
    /// </summary>
    /// <param name="ItemID"></param>
    public void GetItemContent(int ItemID)
    {
        publishedFileID = subscribedItemList[ItemID];
        SteamAPICall_t handle = SteamRemoteStorage.GetPublishedFileDetails(publishedFileID, 0);
        RemoteStorageGetPublishedFileDetailsResult.Set(handle);
    }

    public void DeleteFile(string filename)
    {
        bool ret = SteamRemoteStorage.FileDelete(filename);
    }

    /// <summary>
    /// This functions saves a file to the workshop.
    /// Make sure file size doesn't pass the steamworks limit on your app settings.
    /// </summary>
    /// <param name="fileName">File Name (actual physical file) example: map.txt</param>
    /// <param name="fileData">File Data (actual file data)</param>
    /// <param name="workshopTitle">Workshop Item Title</param>
    /// <param name="workshopDescription">Workshop Item Description</param>
    /// <param name="tags">Tags</param>
    public void SaveToWorkshop(string fileName, string fileData, string workshopTitle, string workshopDescription, string[] tags, string imageLoc)
    {

        lastFileName = fileName;
        bool fileExists = SteamRemoteStorage.FileExists(fileName);

        if (fileExists)
        {
            Debug.Log("Item with that filename already exists");
        }
        else
        {
            bool upload = UploadFile(fileName, fileData);

            if (!upload)
            {
                Debug.Log("Upload cannot be completed");
            }
            else
            {
				//pass in the image location of the file to a global variable so that it doesn't have to be passed in again and again
                thisUploadsIamgeLoc = imageLoc;
                UploadToWorkshop(fileName, workshopTitle, workshopDescription, tags);
            }
        }
    }

    private bool UploadFile(string fileName, string fileData)
    {
        byte[] Data = new byte[System.Text.Encoding.UTF8.GetByteCount(fileData)];
        System.Text.Encoding.UTF8.GetBytes(fileData, 0, fileData.Length, Data, 0);
        bool ret = SteamRemoteStorage.FileWrite(fileName, Data, Data.Length);

        return ret;
    }

    private void UploadToWorkshop(string fileName, string workshopTitle, string workshopDescription, string[] tags)
    {
        SteamAPICall_t handle = SteamRemoteStorage.PublishWorkshopFile(fileName, null, SteamUtils.GetAppID(), workshopTitle, workshopDescription, ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic, tags, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
        RemoteStoragePublishFileResult.Set(handle);
    }

    public void Unsubscribe(PublishedFileId_t file)
    {
        SteamAPICall_t handle = SteamRemoteStorage.UnsubscribePublishedFile(file);
        RemoteStorageUnsubscribePublishedFileResult.Set(handle);
    }

    IEnumerator startPictureUpdate(RemoteStoragePublishFileResult_t pCallback)
    {
		//Wait 1 second just to make sure initial upload is 100% complete. Although technically entering this Coroutine indicated 100% completion. Consider it a fail safe.
        yield return new WaitForSeconds(1.0f);
        UGCUpdateHandle_t m_UGCUpdateHandle = SteamUGC.StartItemUpdate(SteamUtils.GetAppID(), pCallback.m_nPublishedFileId);
        bool ret = SteamUGC.SetItemPreview(m_UGCUpdateHandle, thisUploadsIamgeLoc);
        if (ret)
        {
			Debug.Log("Thumbnail upload intialization success");
            SteamAPICall_t handle = SteamUGC.SubmitItemUpdate(m_UGCUpdateHandle, "Add Screenshot");
            ItemUpdateResult.Set(handle);
        }
        else
        {
            Debug.Log("Thumbnail upload intialization failed, but file upload succeded");
        }
    }

    void OnRemoteStorageUnsubscribePublishedFileResult(RemoteStorageUnsubscribePublishedFileResult_t pCallback, bool bIOFailure)
    {
        //Debug.Log("[" + RemoteStorageUnsubscribePublishedFileResult_t.k_iCallback + " - RemoteStorageUnsubscribePublishedFileResult] - " + pCallback.m_eResult + " -- " + pCallback.m_nPublishedFileId);
    }

    void OnRemoteStoragePublishFileResult(RemoteStoragePublishFileResult_t pCallback, bool bIOFailure)
    {
        if (pCallback.m_eResult == EResult.k_EResultOK)
        {
			Debug.Log("File upload success, starting thumbnail upload");
            StartCoroutine(startPictureUpdate(pCallback));
            publishedFileID = pCallback.m_nPublishedFileId;
            DeleteFile(lastFileName);           
        }
        else
        {
            Debug.Log("File upload failed");
        }
    }

    void OnRemoteStorageEnumerateUserSubscribedFilesResult(RemoteStorageEnumerateUserSubscribedFilesResult_t pCallback, bool bIOFailure)
    {
		//Clear list from last call
        subscribedItemList = new List<PublishedFileId_t>();
        for (int i = 0; i < pCallback.m_nTotalResultCount; i++)
        {
			//fetch subscribed item and add it to the list
            PublishedFileId_t f = pCallback.m_rgPublishedFileId[i];
            subscribedItemList.Add(f);
        }
		//Now that all files have been fetched we need to download them
        StartCoroutine(downloadFiles());
    }

    void OnRemoteStorageGetPublishedFileDetailsResult(RemoteStorageGetPublishedFileDetailsResult_t pCallback, bool bIOFailure)
    {
        if (pCallback.m_eResult == EResult.k_EResultOK)
        {
			//if we were able to get the details of the subscribed item we need to check if we need to update it
            bool overWrite = false;
            if (File.Exists(pCallback.m_pchFileName))
            {
				Debug.Log("File exists so now we check if it's outdated");
				//I'm not sure how correct this is for edge cases but it seems to work.
                uint systemEpoch = (uint)(System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1))).TotalSeconds;
				//maybe it's better to save the pCallback.m_rtimeUpdated when the file is first downloaded.
                if (pCallback.m_rtimeUpdated> systemEpoch)
                {
					//if the file on the workshop is newer then the local file
					//we need to update
                    overWrite = true;
                }
            }
            else
            {
				Debug.Log("File doesn't exist we need to download it");
                overWrite = true;
            }
            if (overWrite)
            {
				//This is where we actually make the callback to download it
                UGCHandle = pCallback.m_hFile;
                SteamAPICall_t handle = SteamRemoteStorage.UGCDownload(UGCHandle, 0);
                RemoteStorageDownloadUGCResult.Set(handle);
            }
            else
            {
                fetchedContent = true;
                Debug.Log("File is up to date and we can now continue to the next one");
            }
        }
        else
        {
			//Unable to get details from the steamworkshop for this file
			//maybe it doesn't exist any more?
            fetchedContent = true;
            
        }
    }

    void OnRemoteStorageDownloadUGCResult(RemoteStorageDownloadUGCResult_t pCallback, bool bIOFailure)
    {
		//finally downloading the file
        byte[] Data = new byte[pCallback.m_nSizeInBytes];
        int ret = SteamRemoteStorage.UGCRead(UGCHandle, Data, pCallback.m_nSizeInBytes, 0, EUGCReadAction.k_EUGCRead_Close);

        itemContent = System.Text.Encoding.UTF8.GetString(Data, 0, ret);
        File.WriteAllText(pCallback.m_pchFileName, itemContent);
		//time to continue to the next one.
        fetchedContent = true;
    }
}