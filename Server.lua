import "E:\\ZYSocketFrame2\\北风之神SOCKET框架(ZYSocket)DLL\\ZYSocketFrame.dll"
import "E:\\ZYSocketFrame2\\北风之神SOCKET框架(ZYSocket)DLL\\ZYSocketShare.dll"

import "System"
import "System.Net.Sockets"
import "ZYSocket.share"
import "ZYSocket.Server"
import "System.Text"

function DataOn(data, e)
    local read = ReadBytesV2(data);
    local b, lengt = read:ReadInt32();

    if(read.Length == lengt) then
        local b, cmd = read:ReadInt32();
        if(cmd == 1000)then

            local b, temp = read:ReadObject(); --fuck is Generic ...
			print("OK");
            if(temp ~= nil)then                
                Console.WriteLine("Port:{4} Id:{0}\r\n Mn:{1} \r\n GuidCount:{2} \r\n DataLength:{3} \r\n\r\n", temp.Id, temp.Message, temp.guidList.Count);
            end
        end

    end

end

function BinaryInputHandler(data, offset, count, socketAsync)
    if(socketAsync.UserToken == nil) then
        socketAsync.UserToken = ZYNetRingBufferPoolV2(4096000);
    end

    local stream = socketAsync.UserToken;

    stream:Write(data, offset, count);

    local res, data = stream:Read()
    while(res == true)
    do
        DataOn(data, socketAsync);

        res = stream:Read()
    end

end

function ConnectionFilter(socketAsyn)
    Console.WriteLine("UserConn {0}", socketAsyn.AcceptSocket.RemoteEndPoint:ToString());
    socketAsyn.UserToken = null;
    return true;
end

function MessageInputHandler(message, socketAsync, error)
    Console.WriteLine(message);
    socketAsync.UserToken = nil;
    socketAsync.AcceptSocket:Close();
    socketAsync.AcceptSocket:Dispose();
end

server = ZYSocketSuper("any", 9982, 5000, 1024 * 1024);
server.BinaryOffsetInput = BinaryInputHandler;
server.Connetions = ConnectionFilter;
server.MessageInput = MessageInputHandler;
server.IsOffsetInput = true;
server:Start();

Console.ReadLine();


