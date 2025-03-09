using SIPSorcery.Net;
using WebSocketSharp;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace jihadkhawaja.chat.server.SIP
{
    public class SDPExchange : WebSocketBehavior
    {
        /// <summary>
        /// The RTCPeerConnection associated with this connection.
        /// </summary>
        public RTCPeerConnection PeerConnection { get; set; }

        /// <summary>
        /// Event triggered when a new WebSocket connection is opened.
        /// The subscriber should create and return a new RTCPeerConnection.
        /// </summary>
        public event Func<WebSocketContext, Task<RTCPeerConnection>> WebSocketOpened;

        /// <summary>
        /// Event triggered when a message is received from the client.
        /// The subscriber can handle the SDP or ICE candidate message.
        /// </summary>
        public event Action<RTCPeerConnection, string> OnMessageReceived;

        protected override async void OnOpen()
        {
            base.OnOpen();
            if (WebSocketOpened != null)
            {
                // Create an RTCPeerConnection and assign it to the property.
                PeerConnection = await WebSocketOpened(this.Context);
            }
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            if (OnMessageReceived != null && PeerConnection != null)
            {
                OnMessageReceived(PeerConnection, e.Data);
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            if (PeerConnection != null)
            {
                PeerConnection.Close("Remote party closed connection");
            }
        }
    }

}
