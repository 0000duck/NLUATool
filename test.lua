require "CLRPackage";
import  "C:\\Users\\luyikk\\Documents\\Visual Studio 2015\\LuaTest\\ZYSocketShare.dll"
import  "C:\\Users\\luyikk\\Documents\\Visual Studio 2015\\LuaTest\\ZYSocketClientB.dll"
import "ZYSocket.share"
import "ZYSocket.ClientB"
import "System.Text"
import "System"

local m = math.cos(199);

print(m);

local x = SocketClient();


if (x:Connect("www.baidu.com", 80) == true) then
    x.BinaryInput:Add(
    function(data)
        local html = Encoding.Default:GetString(data)
        Console.WriteLine(html);
        x:Close();
    end);
    x:StartRead();
    local str = "GET / HTTP/1.1\r\nAccept: *.*\r\n\r\n";
    local byte = Encoding.Default:GetBytes(str);
    x:Send(byte);
    Console.ReadLine();
else
    Console.WriteLine("无法连接服务器");
end




