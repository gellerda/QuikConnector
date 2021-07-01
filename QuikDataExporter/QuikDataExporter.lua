dofile("D:\\mydocs\\LUA\\mylibs\\system.lua"); -- Разные универсальные функции не имеющие отношения к Квику.
dofile("D:\\mydocs\\LUA\\mylibs\\stdquik.lua"); -- Разные универсальные функции не имеющие отношения к Квику.
luaFMConnector = require("LuaFMConnector");

IsRun = true;
SecCatalog={};
DataSources={}; -- Ключ таблицы - строка формата "classCode_secCode_quikTimeFrame"

ObjectsDelimiterSymbol = "*";
ParamsDelimiterSymbol = ";";

QuikDataCode =
{
	DataZero = 0,
	SecCatalog = 1,
	Candles = 2
}

ImporterMessageCode =
{
	MessageZero = 0,
	SendSecCatalog = 1,
	StartCandleExport = 2,
	StopCandleExport = 3,
	StopAllCandlesExport = 4
}
--======================================================================================================================
function OnStop(s)
    IsRun = false;
end
--======================================================================================================================
function InitSecCatalog(class_codes) -- class_codes - коды классов через запятую
	for class_code in string.gmatch(class_codes, "[^,]+") do
		class_code = Trim(class_code);
		local sec_list = getClassSecurities(class_code);
		
		for sec_code in string.gmatch(sec_list, "[^,]+") do
			sec_code=Trim(sec_code);
			sec_info = getSecurityInfo(class_code, sec_code);
			--message (sec_code..", name="..sec_info["name"]..", short_name="..sec_info["short_name"]);
			table.insert(SecCatalog, {class_code=class_code, sec_code=sec_code, name=sec_info["name"], candleDataDources={}});
		end
	end
end
--======================================================================================================================
_quikDataToExportQueue = {};

function QuikDataToExportQueue_IsEmpty()
	return (next(_quikDataToExportQueue) == nil);
end

function QuikDataToExportQueue_GetFirstWithoutRemoving()
	if next(_quikDataToExportQueue) ~= nil then
		local qDataToExport = _quikDataToExportQueue[1];
		local qDataCode = qDataToExport[1];
		local qDataString = qDataToExport[2];
		return qDataCode, qDataString;
	end
	
	return -1, "";
end

function QuikDataToExportQueue_RemoveFirst()
	if next(_quikDataToExportQueue) ~= nil then
		table.remove(_quikDataToExportQueue, 1);
	end
end

function QuikDataToExportQueue_Enqueue(quikDataCode, quikDataString)
	table.insert(_quikDataToExportQueue, {quikDataCode, quikDataString});
	PrintDbgStr("Quik Data has been added to Export Queue, quikDataCode="..quikDataCode..", quikDataStringLength="..#quikDataString..", quikDataString="..quikDataString);
end

function QuikDataToExportQueue_Clear()
	_quikDataToExportQueue = {};
end
--======================================================================================================================
function ProcessImporterMessage(importerMessageCode, importerMessageParams)
	if importerMessageCode==ImporterMessageCode.SendSecCatalog then
		ProcessImporterMessage_SendSecCatalog();
	elseif importerMessageCode==ImporterMessageCode.StartCandleExport then
		ProcessImporterMessage_StartCandleExport(importerMessageParams);
	elseif importerMessageCode==ImporterMessageCode.StopCandleExport then
		ProcessImporterMessage_StopCandleExport(importerMessageParams);
	elseif importerMessageCode==ImporterMessageCode.MessageZero then
		ProcessImporterMessage_MessageZero();
	end
end

function ProcessImporterMessage_MessageZero()
	for _, ds in pairs(DataSources) do
		ds:Close();
	end
	DataSources={};
	QuikDataToExportQueue_Clear();
end

function CreateQuikDataString_SendSecCatalog()
	local quikDataString="";
	
	local sec;
	for i = 1, #SecCatalog, 1 do
		sec = SecCatalog[i];
		
		if i > 1 then
			quikDataString = quikDataString..ObjectsDelimiterSymbol;
		end
		
		quikDataString = quikDataString..sec.class_code..ParamsDelimiterSymbol..sec.sec_code..ParamsDelimiterSymbol..sec.name;
	end
	
	return quikDataString;
end

function ProcessImporterMessage_SendSecCatalog()
	QuikDataToExportQueue_Enqueue(QuikDataCode.SecCatalog, CreateQuikDataString_SendSecCatalog());
end

function ProcessImporterMessage_StartCandleExport(importerMessageParams)
	local quikDataString="";
	local maxQuikDataStringLength = luaFMConnector.GetQuikDataFileMappingObjectSize() - 1000 ;
	
	for secToExportParams in string.gmatch(importerMessageParams, "[^"..ObjectsDelimiterSymbol.."]+") do
		local classCode_secCode_tf = Trim(secToExportParams); -- secToExportParams := "classCode_secCode_quikTimeFrame"
		local ds = CreateDataSourceAndSetCallback(classCode_secCode_tf);
		DataSources[classCode_secCode_tf] = ds;
		local size = ds:Size(); -- Количество свечей в источнике данных 
		
		for i = 1, size do
			quikDataString = quikDataString..IIF(quikDataString ~= "", ObjectsDelimiterSymbol, "")..QuikDataStringForOneCandleExport(classCode_secCode_tf, i);
			
			if string.len(quikDataString) > maxQuikDataStringLength then
				QuikDataToExportQueue_Enqueue(QuikDataCode.Candles, quikDataString);
				quikDataString = "";
			end
		end
	end
	
	QuikDataToExportQueue_Enqueue(QuikDataCode.Candles, quikDataString);
end

function ProcessImporterMessage_StopCandleExport(importerMessageParams)
	local quikDataString="";
	local maxQuikDataStringLength = luaFMConnector.GetQuikDataFileMappingObjectSize() - 1000 ;
	
	for secToExportParams in string.gmatch(importerMessageParams, "[^"..ObjectsDelimiterSymbol.."]+") do
		local classCode_secCode_tf = Trim(secToExportParams); -- secToExportParams := "classCode_secCode_quikTimeFrame"
		local ds = DataSources[classCode_secCode_tf];
		
		if ds~=nil then
			DataSources[classCode_secCode_tf] = nil;
			--ds:SetEmptyCallback();
			ds:Close(); -- Закрываем источник данных		
		end
	end
end

function CreateDataSourceAndSetCallback(classCode_secCode_tf)
		local params = SplitString(classCode_secCode_tf, "_");
		local class_code = params[1]; 		
		local sec_code = params[2]; 		
		local time_frame = tonumber(params[3]); 		

		local ds, err;
		ds, err = CreateDataSource(class_code, sec_code, time_frame);
		
		-- Ограничиваем время ожидания подключения к источнику данных и загрузки данных с сервера
		local try_N = 10;
		local try_i = 0;
		-- Ждем пока не получим данные от сервера,
		--	либо пока не закончится время ожидания
		while (err == "" or err == nil) and ds:Size() == 0 and try_i < try_N do
			sleep(1000);
			try_i = try_i + 1;
		end
		
		-- Если от сервера пришла ошибка, то выведем ее и прервем выполнение:
		if err ~= nil and err ~= "" then
			PrintDbgStr("Ошибка подключения к источнику данных: "..err);
			return "Ошибка подключения к источнику данных: "..err;
		-- Если истекло время ожидания:
		elseif ds == nil or ds:Size() == 0 then
			PrintDbgStr("Истекло время ожидания данных от сервера");
			return "Истекло время ожидания данных от сервера" ;
		end
		
		ds:SetUpdateCallback(function(...) OnCandleDataSourceChanged(classCode_secCode_tf,...) end)
		
		return ds;
end

function OnCandleDataSourceChanged(classCode_secCode_tf, cndl_index) 
	local quikDataString = QuikDataStringForOneCandleExport(classCode_secCode_tf, cndl_index)
	PrintDbgStr("Candle changed. classCode_secCode_tf="..classCode_secCode_tf..", i="..cndl_index..", quikDataString="..quikDataString)
	QuikDataToExportQueue_Enqueue(QuikDataCode.Candles, quikDataString);
end

function QuikDataStringForOneCandleExport(classCode_secCode_tf, cndl_index)
	local ds = DataSources[classCode_secCode_tf];
	local secInfo = SecCatalog[secCatalog_i];
	local data_string = 
			classCode_secCode_tf..ParamsDelimiterSymbol..
			(cndl_index - 1)..ParamsDelimiterSymbol..
			ds:T(cndl_index).year..ParamsDelimiterSymbol..
			ds:T(cndl_index).month..ParamsDelimiterSymbol..
			ds:T(cndl_index).day..ParamsDelimiterSymbol..
			ds:T(cndl_index).hour..ParamsDelimiterSymbol..
			ds:T(cndl_index).min..ParamsDelimiterSymbol..
			ds:T(cndl_index).sec..ParamsDelimiterSymbol..
			ds:T(cndl_index).ms..ParamsDelimiterSymbol..
			ds:O(cndl_index)..ParamsDelimiterSymbol..
			ds:H(cndl_index)..ParamsDelimiterSymbol..
			ds:L(cndl_index)..ParamsDelimiterSymbol..
			ds:C(cndl_index)..ParamsDelimiterSymbol..
			ds:V(cndl_index);
			
	return data_string;
end 
--======================================================================================================================
function main()
	InitSecCatalog("SPBFUT,TQBR");
	
	message("start="..tostring(luaFMConnector.StartExport(QuikDataCode.DataZero, "")));
	
	local msgCode, msgParams;
	while IsRun do
		msgCode, msgParams = luaFMConnector.ReceiveImporterMessage();
		
		if msgCode ~= -1 then
			PrintDbgStr("Has got importer message: msgCode="..msgCode..", paramsStringLength="..#msgParams..", paramsString="..msgParams);	
			ProcessImporterMessage(msgCode, msgParams);	
		end
		
		if not QuikDataToExportQueue_IsEmpty() then
			local dataCode, dataString = QuikDataToExportQueue_GetFirstWithoutRemoving();
			--PrintDbgStr("Try to send data: dataCode="..dataCode..", stringLen="..IIF(dataString==nil,"",#dataString)..", dataString="..IIF(dataString==nil,"",dataString));
			if luaFMConnector.SendQuikData(dataCode, dataString) then
				PrintDbgStr("Data has been sent: dataCode="..dataCode..", stringLen="..IIF(dataString==nil,"-1",#dataString)..", dataString="..NilToStr(dataString));
				QuikDataToExportQueue_RemoveFirst();
			end
		end
	end
	
	luaFMConnector.StopExport();
end
--======================================================================================================================
--**********************************************************************************************************************************************************************
--**********************************************************************************************************************************************************************
--**********************************************************************************************************************************************************************
--======================================================================================================================
function IIF(condition, true_result, false_result)
	if condition then return true_result; else return false_result;	end
end
--======================================================================================================================
function NilToStr(x)
	if x==nil then return "nil"
	else return x;
	end
end
--======================================================================================================================
function Trim(s)
   return (s:gsub("^%s*(.-)%s*$", "%1"))
end
--======================================================================================================================
function SplitString(stringToSplit, separator)
        if separator == nil then
                separator = "%s"
        end
        local t={}
        for str in string.gmatch(stringToSplit, "([^"..separator.."]+)") do
                table.insert(t, str)
        end
        return t
end
--======================================================================================================================
