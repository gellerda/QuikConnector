#include <windows.h>
#include <stdio.h>

//=== Необходимые для Lua константы ============================================================================
#define LUA_LIB
#define LUA_BUILD_AS_DLL

//=== Заголовочные файлы LUA ===================================================================================
extern "C" {
#include "Lua\Lua53\include\lauxlib.h"
#include "Lua\Lua53\include\lua.h"
}

DWORD MemoryMappedFileSize_QuikData = 8000000;
DWORD MemoryMappedFileSize_ImporterMessages = 30000;

TCHAR fileMappingObjectName_QuikData[] = TEXT("LuaFileMapping_QuikData");
TCHAR fileMappingObjectName_ImporterMessages[] = TEXT("LuaFileMapping_ImporterMessages");
HANDLE fileMappingObject_QuikData;
HANDLE fileMappingObject_ImporterMessages;
PBYTE fileView_QuikData;
PBYTE fileView_ImporterMessages;

LPCTSTR eventName_QuikDataHasBeenSent = L"LuaEvent_QuikDataHasBeenSent";
LPCTSTR eventName_QuikDataHasBeenReceived = L"LuaEvent_QuikDataHasBeenReceived";
LPCTSTR eventName_ImporterMessageHasBeenSent = L"LuaEvent_ImporterMessageHasBeenSent";
LPCTSTR eventName_ImporterMessageHasBeenReceived = L"LuaEvent_ImporterMessageHasBeenReceived";
HANDLE event_QuikDataHasBeenSent;
HANDLE event_QuikDataHasBeenReceived;
HANDLE event_ImporterMessageHasBeenSent;
HANDLE event_ImporterMessageHasBeenReceived;

//=== Стандартная точка входа для DLL ==========================================================================
BOOL APIENTRY DllMain(HANDLE hModule, DWORD  fdwReason, LPVOID lpReserved)
{
	//Каждому событию соответствует свое значение аргумента fdwReason, передаваемого функции DllMain при его возникновении   
	switch (fdwReason)
	{
	case DLL_PROCESS_ATTACH: // Подключение DLL          
		break;
	case DLL_PROCESS_DETACH: // Отключение DLL
		break;
	case DLL_THREAD_ATTACH:  // Создание нового потока
		break;
	case DLL_THREAD_DETACH:  // Завершение потока
		break;
	}
	return TRUE;
}
//---------------------------------------------------------------------------------------------------------------------------------
static int forLua_GetQuikDataFileMappingObjectSize(lua_State* L)
{
	lua_pushinteger(L, MemoryMappedFileSize_QuikData);
	return 1;
}
//---------------------------------------------------------------------------------------------------------------------------------
static int forLua_GetImporterMessagesFileMappingObjectSize(lua_State* L)
{
	lua_pushinteger(L, MemoryMappedFileSize_ImporterMessages);
	return 1;
}
//---------------------------------------------------------------------------------------------------------------------------------
static int forLua_StartExport(lua_State* L)
{
	fileMappingObject_QuikData = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, MemoryMappedFileSize_QuikData, fileMappingObjectName_QuikData); // Создаст, или подключится к уже созданной памяти с таким именем
	if (fileMappingObject_QuikData == NULL)
		luaL_error(L, "Could not create a named file mapping object for QuikData.");

	fileMappingObject_ImporterMessages = CreateFileMapping(INVALID_HANDLE_VALUE, NULL, PAGE_READWRITE, 0, MemoryMappedFileSize_ImporterMessages, fileMappingObjectName_ImporterMessages); // Создаст, или подключится к уже созданной памяти с таким именем
	if (fileMappingObject_ImporterMessages == NULL)
		luaL_error(L, "Could not create a named file mapping object for ImporterMessages.");

	fileView_QuikData = (PBYTE)(MapViewOfFile(fileMappingObject_QuikData, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 0));
	if (fileView_QuikData == NULL)
		luaL_error(L, "Could not create a file view for QuikData.");

	fileView_ImporterMessages = (PBYTE)(MapViewOfFile(fileMappingObject_ImporterMessages, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, 0));
	if (fileView_ImporterMessages == NULL)
		luaL_error(L, "Could not create a file view for ImporterMessages.");

	// HANDLE CreateEvent(LPSECURITY_ATTRIBUTES lpEventAttributes, BOOL bManualReset, BOOL bInitialState, LPCTSTR lpName)
	event_QuikDataHasBeenSent = CreateEvent(NULL, FALSE, FALSE, eventName_QuikDataHasBeenSent);  // Если объект ядра с таким именем уже существует, то он откроется. Иначе - создастся новый объект ядра.
	if (event_QuikDataHasBeenSent == NULL || event_QuikDataHasBeenSent == INVALID_HANDLE_VALUE) // Разные объекты ядра возвращают либо то, либо другое при ошибке создания. Нужно читать документацию.
		luaL_error(L, "Could not create the DataHasBeenSent event.");
	//SetEvent(event_QuikDataHasBeenSent);

	event_QuikDataHasBeenReceived = CreateEvent(NULL, TRUE, FALSE, eventName_QuikDataHasBeenReceived);  // Если объект ядра с таким именем уже существует, то он откроется. Иначе - создастся новый объект ядра.
	if (event_QuikDataHasBeenReceived == NULL || event_QuikDataHasBeenReceived == INVALID_HANDLE_VALUE) // Разные объекты ядра возвращают либо то, либо другое при ошибке создания. Нужно читать документацию.
		luaL_error(L, "Could not create the DataHasBeenReceived event.");
	//Всегда можем определить был ли создан новый объект ядра или открыт уже существующий с таким именем:
	//if (GetLastError() == ERROR_ALREADY_EXISTS) {} // Открыт существующий объект ядра. lpSecurityDescriptor при этом игнорируется.
	//else {} // Создан новый объект ядра. lpSecurityDescriptor учтен при создании объекта.

	event_ImporterMessageHasBeenSent = CreateEvent(NULL, FALSE, FALSE, eventName_ImporterMessageHasBeenSent);  // Если объект ядра с таким именем уже существует, то он откроется. Иначе - создастся новый объект ядра.
	if (event_ImporterMessageHasBeenSent == NULL || event_ImporterMessageHasBeenSent == INVALID_HANDLE_VALUE) // Разные объекты ядра возвращают либо то, либо другое при ошибке создания. Нужно читать документацию.
		luaL_error(L, "Could not create the ImporterMessageHasBeenSent event.");
	//SetEvent(event_ImporterMessageHasBeenSent);

	event_ImporterMessageHasBeenReceived = CreateEvent(NULL, TRUE, TRUE, eventName_ImporterMessageHasBeenReceived);  // Если объект ядра с таким именем уже существует, то он откроется. Иначе - создастся новый объект ядра.
	if (event_ImporterMessageHasBeenReceived == NULL || event_ImporterMessageHasBeenReceived == INVALID_HANDLE_VALUE) // Разные объекты ядра возвращают либо то, либо другое при ошибке создания. Нужно читать документацию.
		luaL_error(L, "Could not create the ImporterMessageHasBeenReceived event.");







	//forLua_SendQuikData(L);
	DWORD dwWaitResult = WaitForSingleObject(event_QuikDataHasBeenReceived, 0);
	if (dwWaitResult == WAIT_OBJECT_0)
	{
		ResetEvent(event_QuikDataHasBeenReceived);

		BYTE quikDataCode = lua_tonumber(L, 1);
		BYTE* ptrQuikDataCode = &quikDataCode;

		size_t quikDataLength;
		const char* quikData = luaL_checklstring(L, 2, &quikDataLength);
		int intQuikDataLength = quikDataLength;
		int* ptrIntQuikDataLength = &intQuikDataLength;

		memcpy(fileView_QuikData, ptrQuikDataCode, 1);
		memcpy(fileView_QuikData + 1, ptrIntQuikDataLength, 4);
		memcpy(fileView_QuikData + 5, quikData, intQuikDataLength);

		lua_pushboolean(L, true);

		SetEvent(event_QuikDataHasBeenSent);
	}
	else if (dwWaitResult == WAIT_TIMEOUT)
	{
		lua_pushboolean(L, false);
	}
	else // (dwWaitResult == WAIT_FAILED)
		luaL_error(L, "Waiting for signal (event_QuikDataHasBeenReceived) error.");









	return(1);
}
//---------------------------------------------------------------------------------------------------------------------------------
static int forLua_StopExport(lua_State* L)
{
	if (fileView_QuikData != NULL)
		UnmapViewOfFile(fileView_QuikData);
	if (fileView_ImporterMessages != NULL)
		UnmapViewOfFile(fileView_ImporterMessages);

	if (fileMappingObject_QuikData != NULL)
		CloseHandle(fileMappingObject_QuikData);
	if (fileMappingObject_ImporterMessages != NULL)
		CloseHandle(fileMappingObject_ImporterMessages);

	CloseHandle(event_QuikDataHasBeenSent); // Уменьшает счетчик числа пользователей для данного объекта ядра. Когда счетчик достигнет нуля, ядро ОС самостоятельно удалит данный объект ядра.
	CloseHandle(event_QuikDataHasBeenReceived); // Уменьшает счетчик числа пользователей для данного объекта ядра. Когда счетчик достигнет нуля, ядро ОС самостоятельно удалит данный объект ядра.
	CloseHandle(event_ImporterMessageHasBeenSent); // Уменьшает счетчик числа пользователей для данного объекта ядра. Когда счетчик достигнет нуля, ядро ОС самостоятельно удалит данный объект ядра.
	CloseHandle(event_ImporterMessageHasBeenReceived); // Уменьшает счетчик числа пользователей для данного объекта ядра. Когда счетчик достигнет нуля, ядро ОС самостоятельно удалит данный объект ядра.

	event_QuikDataHasBeenSent = NULL; // На всякий случай, чтобы случайно не использовать этот дескриптор объекта.
	event_QuikDataHasBeenReceived = NULL; // На всякий случай, чтобы случайно не использовать этот дескриптор объекта.
	event_ImporterMessageHasBeenSent = NULL; // На всякий случай, чтобы случайно не использовать этот дескриптор объекта.
	event_ImporterMessageHasBeenReceived = NULL; // На всякий случай, чтобы случайно не использовать этот дескриптор объекта.
	return(0);
}
//---------------------------------------------------------------------------------------------------------------------------------
// Если событие event_QuikDataHasBeenReceived установлено, то записывает данные в разделяемую память и устанавливает событие event_QuikDataHasBeenSent. Не останавливает поток.
// bool SendQuikData(QuikDataCode quikDataCode, string quikData)
// Возвращает: true если удалось отправить данные. 
static int forLua_SendQuikData(lua_State* L)
{
	if (fileMappingObject_QuikData == NULL)
		luaL_error(L, "File mapping object QuikData has not been created.");

	if (fileView_QuikData == NULL)
		luaL_error(L, "File view QuikData has not been created.");

	DWORD dwWaitResult = WaitForSingleObject(event_QuikDataHasBeenReceived, 0);
	if (dwWaitResult == WAIT_OBJECT_0)
	{
		ResetEvent(event_QuikDataHasBeenReceived);

		BYTE quikDataCode = lua_tonumber(L, 1);
		BYTE* ptrQuikDataCode = &quikDataCode;

		size_t quikDataLength;
		const char* quikData = luaL_checklstring(L, 2, &quikDataLength);
		int intQuikDataLength = quikDataLength;
		int* ptrIntQuikDataLength = &intQuikDataLength;

		memcpy(fileView_QuikData, ptrQuikDataCode, 1);
		memcpy(fileView_QuikData + 1, ptrIntQuikDataLength, 4);
		memcpy(fileView_QuikData + 5, quikData, intQuikDataLength);

		lua_pushboolean(L, true);

		SetEvent(event_QuikDataHasBeenSent);
	}
	else if (dwWaitResult == WAIT_TIMEOUT)
	{
		lua_pushboolean(L, false);
	}
	else // (dwWaitResult == WAIT_FAILED)
		luaL_error(L, "Waiting for signal (event_QuikDataHasBeenReceived) error.");

	return(1);
}
//---------------------------------------------------------------------------------------------------------------------------------
// Если событие event_ImporterMessageHasBeenSent установлено, то читает сообщение из разделяемой памяти и устанавливает событие event_ImporterMessageHasBeenReceived. Не останавливает поток.
// Входных параметров нет. Возвращает два значения:
// Значение 1: -1 - Импортер еще не не отправил сообщение. Иначе - ImporterMessageCode отправленного импортером сообщения.
// Значение 2: Строка с параметрами отправленного сообщения, если они есть.
static int forLua_ReceiveImporterMessage(lua_State* L)
{
	if (fileMappingObject_ImporterMessages == NULL)
		luaL_error(L, "File mapping object ImporterMessages has not been created.");

	if (fileView_ImporterMessages == NULL)
		luaL_error(L, "File view ImporterMessages has not been created.");

	DWORD dwWaitResult = WaitForSingleObject(event_ImporterMessageHasBeenSent, 0);
	if (dwWaitResult == WAIT_OBJECT_0)
	{
		lua_pushinteger(L, fileView_ImporterMessages[0]);

		int paramsStringLength;
		int* ptrParamsStringLength = &paramsStringLength;
		memcpy(ptrParamsStringLength, fileView_ImporterMessages + 1, 4);

		const char* ptr = reinterpret_cast<const char*>(fileView_ImporterMessages);
		lua_pushlstring(L, ptr + 5, paramsStringLength);

		SetEvent(event_ImporterMessageHasBeenReceived);
	}
	else if (dwWaitResult == WAIT_TIMEOUT)
	{
		lua_pushinteger(L, -1);
		lua_pushstring(L, "");
	}
	else // (dwWaitResult == WAIT_FAILED)
		luaL_error(L, "Waiting for signal (event_ImporterMessageHasBeenSent) error.");

	return(2);
}
//---------------------------------------------------------------------------------------------------------------------------------
// Регистрация реализованных в dll функций, чтобы они стали "видимы" для Lua
static struct luaL_Reg ls_lib[] =
{
   {"GetQuikDataFileMappingObjectSize", forLua_GetQuikDataFileMappingObjectSize},
   {"GetImporterMessagesFileMappingObjectSize", forLua_GetImporterMessagesFileMappingObjectSize},
   {"StartExport", forLua_StartExport},
   {"StopExport", forLua_StopExport},
   {"SendQuikData", forLua_SendQuikData},
   {"ReceiveImporterMessage", forLua_ReceiveImporterMessage},
   {NULL, NULL}
};
//---------------------------------------------------------------------------------------------------------------------------------
// Регистрация названия библиотеки, видимого в скрипте Lua
extern "C" LUALIB_API int luaopen_LuaFMConnector(lua_State * L)
{
	// эта функция выполнится в момент вызова require() в Lua-коде
	// регистрируем реализованные в dll функции, чтобы они стали дуступны для Lua
	// в Lua 5.1 и Lua 5.3 для этого предназначены разные функции
#if LUA_VERSION_NUM >= 502
	luaL_newlib(L, ls_lib);
#else
	luaL_openlib(L, "LuaFMConnector", ls_lib, 0);
#endif

	return 1;
}
//---------------------------------------------------------------------------------------------------------------------------------
//---------------------------------------------------------------------------------------------------------------------------------
//---------------------------------------------------------------------------------------------------------------------------------
//---------------------------------------------------------------------------------------------------------------------------------
