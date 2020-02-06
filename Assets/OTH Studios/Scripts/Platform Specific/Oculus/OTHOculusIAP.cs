using UnityEngine;
using Oculus.Platform;
using Oculus.Platform.Models;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets.ResourceProviders;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.Serialization;

namespace OTHStudios
{
    // This class coordinates In-App-Purchases (IAP) for the application
    public class OTHIAPManager : MonoBehaviour
    {
        
#if DISABLESTEAMWORKS

        [Tooltip("The SKU of configured Oculus IAP product")]
        [SerializeField] private List<string> skus;

        [Tooltip("Purchasable IAP products we've configured on the Oculus Dashboard")]
        private AssetDetailsList assetList;

        void Start()
        {
            AssetFile.SetDownloadUpdateNotificationCallback(DownloadUpdateNotificationCallback);
            PopulateAssetList();
            FetchProductPrices();
            FetchPurchasedProducts();
            StartCoroutine(OnStartDownloadEntitled());
        }

        public void PopulateAssetList()
        {
            AssetFile.GetList().OnComplete(GetAvailiableAssetFilesCallback);
        }

        void GetAvailiableAssetFilesCallback(Message<AssetDetailsList> msg)
        {
            if (msg.IsError)
            {
                Debug.LogError(msg.GetError().Message, this);
                return;
            }

            assetList = msg.GetAssetDetailsList();
        }

        /// <summary>
        /// Get the current price for the configured IAP item. Writes to m_priceText property
        /// </summary>
        /// <returns>Returns Nothing</returns>
        public void FetchProductPrices()
        {
            IAP.GetProductsBySKU(skus.ToArray()).OnComplete(GetProductsBySKUCallback);
        }

        private void GetProductsBySKUCallback(Message<ProductList> msg)
        {
            if (msg.IsError)
            {
                Debug.LogError(msg.GetError().Message, this);
                return;
            }

            foreach (Product p in msg.GetProductList())
            {
                Debug.LogFormat("Product: sku:{0} name:{1} price:{2}", p.Sku, p.Name, p.FormattedPrice);
            }
        }

        /// <summary>
        /// Fetches the Durable purchased IAP items.  should return none unless you are expanding the to sample to include them.
        /// </summary>
        /// <returns>Returns Nothing</returns>
        public void FetchPurchasedProducts()
        {
            IAP.GetViewerPurchases().OnComplete(GetViewerPurchasesCallback);
        }

        private void GetViewerPurchasesCallback(Message<PurchaseList> msg)
        {
            if (msg.IsError)
            {
                Debug.LogError(msg.GetError().Message, this);
                return;
            }

            foreach (Purchase p in msg.GetPurchaseList())
            {
                Debug.LogFormat("Purchased: sku:{0} granttime:{1} id:{2}", p.Sku, p.GrantTime, p.ID);
            }
        }

        /// <summary>
        /// Handels IAP for  Oculus
        /// </summary>
        /// <param name="index">The index of the item to purchase</param>  
        public void BuyItem(int index)
        {
#if UNITY_EDITOR
            // Give access to all assets if in editor, as checkout is not supported
            DownloadAllAvailable(false);
#else
            IAP.LaunchCheckoutFlow(skus[index]).OnComplete(LaunchCheckoutFlowCallback);
#endif
        }

        private void LaunchCheckoutFlowCallback(Message<Purchase> msg)
        {
            if (msg.IsError)
            {
                Debug.LogError(msg.GetError().Message, this);
                return;
            }

            Purchase p = msg.GetPurchase();
            Debug.Log("Purchased " + p.Sku);

            // Download the asset file from Oculus
            AssetFile.DownloadById(p.ID).OnComplete(DownloadAssetFileCallback);
        }

        private void DownloadAssetFileCallback(Message<AssetFileDownloadResult> msg)
        {
            if (msg.IsError)
            {
                Debug.LogError(msg.GetError().Message, this);
                return;
            }

            AssetFileDownloadResult result = msg.GetAssetFileDownloadResult();
            Debug.Log("Asset file downloaded to: " + result.Filepath);

            // Parse Addressable key
            string[] temp = result.Filepath.Split('\\');
            string key = temp[temp.Length - 1];

            // Add filepath and parsed key to the Addressable resource locators
            List<ResourceLocationData> locationData = new List<ResourceLocationData>();
            locationData.Add(new ResourceLocationData(
                new string[] { key }, result.Filepath, typeof(AssetBundleProvider), typeof(AssetBundle)));
            ResourceLocationMap locMap = new ResourceLocationMap(result.Filepath, locationData);
            
            Addressables.AddResourceLocator(locMap);

            // if (new AssetReference(AssetDatabase.AssetPathToGUID(newPath)).RuntimeKeyIsValid())
            //     Debug.Log("FUCKING AYYYYYYY MATE");
            // else
            //     Debug.LogError("Invalid AssetReference", this);

            PopulateAssetList();
    }

    private void DownloadUpdateNotificationCallback(Message<AssetFileDownloadUpdate> msg)
    {
        if (msg.IsError)
        {
            Debug.LogError(msg.GetError().Message, this);
            return;
        }

        AssetFileDownloadUpdate update = msg.GetAssetFileDownloadUpdate();
        //m_priceText.text = "Download: " + update.BytesTransferred + " / " + update.BytesTotal;
        Debug.Log("Download: " + update.BytesTransferred + " / " + update.BytesTotal);
    }

    /// <summary>
    /// Downloads all availiable (uninstalled) DLC asset files  
    /// </summary>
    /// <param name="checkEntitlement">True to check if the player owns the asset file or is free prior to downloading</param>
    private void DownloadAllAvailable(bool checkEntitlement)
    {
        foreach (AssetDetails ad in assetList)
        {
            if (checkEntitlement && ad.IapStatus == "not-entitled")
                continue;

            if (ad.DownloadStatus != "installed" && ad.DownloadStatus != "in-progress")
                AssetFile.DownloadById(ad.AssetId).OnComplete(DownloadAssetFileCallback);
        }
    }

    private IEnumerator OnStartDownloadEntitled()
    {
        while (assetList == null)
        {
            yield return null;
        }

        DownloadAllAvailable(true);
        yield return null;
    }
#endif
    }
}
