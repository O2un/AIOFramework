using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace O2un.Core.Network
{
    public static class P2PMessengerExtensions
    {
        /// <summary>
        /// 응답 실패를 예외가 아니라 <see cref="P2PPacketResult{T}"/> 값으로 돌려준다.
        /// Messenger 가 없는 상태(세션 미개설·종료)도 같은 실패 값으로 접는다.
        /// </summary>
        public static async UniTask<P2PPacketResult<TResponse>> SendDataAndWaitResultAsync<TRequest, TResponse>(
            this IP2PMessenger messenger,
            NetworkPacketId eventId,
            NetworkPacketType packetType,
            TRequest data,
            TimeSpan timeout,
            CancellationToken ct = default)
        {
            if (null == messenger)
            {
                return P2PPacketResult<TResponse>.Failure(P2PPacketReasons.NOT_CONNECTED);
            }

            try
            {
                TResponse response = await messenger.SendDataAndWaitAsync<TRequest, TResponse>(eventId, packetType, data, timeout, ct);
                return P2PPacketResult<TResponse>.Success(response);
            }
            catch (NetworkSendFailedException)
            {
                return P2PPacketResult<TResponse>.Failure(P2PPacketReasons.SEND_FAILED);
            }
            catch (TimeoutException)
            {
                return P2PPacketResult<TResponse>.Failure(P2PPacketReasons.TIMEOUT);
            }
            catch (ObjectDisposedException)
            {
                return P2PPacketResult<TResponse>.Failure(P2PPacketReasons.SESSION_CLOSED);
            }
            catch (OperationCanceledException) when (false == ct.IsCancellationRequested)
            {
                // 호출부가 취소한 게 아니면 세션이 응답 대기 중에 끊긴 것이다. 호출부의 취소만 그대로 올려보낸다.
                return P2PPacketResult<TResponse>.Failure(P2PPacketReasons.SESSION_CLOSED);
            }
        }
    }
}
