require "CLRPackage"
import "System"
import "System.Collections.Generic"

ListType = luanet.import_type('System.Collections.Generic.List`1')
String = luanet.import_type('System.String')

string_list, err = MakeGenericType(ListType, String)
string_list:Add("sdf");

