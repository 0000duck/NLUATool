import "N:\\NLUA\\VsTool\\NLUATool\\LuaTest\\ZYSocketFrame.dll"
import "N:\\NLUA\\VsTool\\NLUATool\\LuaTest\\ZYSocketShare.dll"
import "N:\\NLUA\\VsTool\\NLUATool\\LuaTest\\testClass.dll";
import "System"
import "System.Net.Sockets"
import "ZYSocket.share"
import "ZYSocket.Server"
import "System.Text"
import "testClass"

server = ZYSocketSuper("any", 9982, 5000, 1024 * 1024);

function DataOn(data, e)
    local read = ReadBytesV2(data);
    local b, lengt = read:ReadInt32();

    if(read.Length == lengt) then
        local b, cmd = read:ReadInt32();

        if(cmd == 1000) then
            local temp, err = CallGenricMethod(read, "ReadObject", luanet.make_array(Object, { luanet.import_type('testClass.PPo')}));
            if(temp ~= nil)then
                Console.WriteLine("1000 Port:{4} Id:{0}\r\n Mn:{1} \r\n GuidCount:{2} \r\n DataLength:{3} \r\n\r\n", temp.Id, temp.Message, temp.guidList.Count, read.Length, e.AcceptSocket.RemoteEndPoint.Port);
            end
        elseif(cmd == 1001)then
            local b, id = read:ReadInt32();
            local mn, b = read:ReadString();
            local temp, err = CallGenricMethod(read, "ReadObject", luanet.make_array(Object, { luanet.import_type('System.Guid')}));
            if(temp ~= nil)then
                Console.WriteLine("1001 Port:{4} Id:{0}\r\n Mn:{1} \r\n Guid:{2} \r\n DataLength:{3} \r\n\r\n", id, mn, guid, read.Length, e.AcceptSocket.RemoteEndPoint.Port);

            end
        elseif(cmd == 1002)then
            local b, id = read:ReadInt32();
            local mn, b = read:ReadString();
            local guid, b = read:ReadString();
            Console.WriteLine("1002 Id:{0} Mn:{1} guid:{2}", id, mn, guid);
        elseif(cmd == 1003)then
            server.SendData(e.AcceptSocket, data);
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


server.BinaryOffsetInput = BinaryInputHandler;
server.Connetions = ConnectionFilter;
server.MessageInput = MessageInputHandler;
server.IsOffsetInput = true;
server:Start();

Console.ReadLine();


