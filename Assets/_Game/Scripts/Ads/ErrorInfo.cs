using SpektraGames.SpektraUtilities.Runtime;
using UnityEngine;

namespace Ads
{
    public class ErrorInfo
    {
        // TODO: Common properties of MaxSdk.ErrorInfo will be added here
        public string Message { get; private set; }

        public object ErrorDataObject { get; }

        public ErrorInfo(object errorData, string message)
        {
            ErrorDataObject = errorData;
            Message = message;
        }

        /// <summary>
        /// Retrieves the ad data in the desired format.
        /// </summary>
        public T GetErrorData<T>() where T : class
        {
            var data = ErrorDataObject as T; // TODO: Possible garbage if used occasionally
            if (data == null)
            {
                Debug.LogError($"Failed to cast ErrorData to {typeof(T)}");
            }

            return data;
        }

        public override string ToString()
        {
            string msg = "";
            if (!string.IsNullOrEmpty(Message))
                msg += Message + " | ";

            if (ErrorDataObject != null)
            {
                try
                {
                    msg += ErrorDataObject.ToString() + " | ";
                }
                catch
                {
                    // ignored
                }

                try
                {
                    msg += ErrorDataObject.SerializeObject();
                }
                catch
                {
                    // ignored
                }
            }

            return msg;
        }
    }
}