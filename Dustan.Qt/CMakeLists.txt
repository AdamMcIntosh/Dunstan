cmake_minimum_required(VERSION 3.21)

project(Dustan VERSION 2.0.0 DESCRIPTION "NAS music and video player" LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(DUSTAN_VERSION "${PROJECT_VERSION}" CACHE STRING "Version stamped into the app and bridge (release tags override it)")
option(DUSTAN_BUILD_BRIDGE "Publish the .NET bridge (Dustan.Bridge) next to the executable" ON)
option(DUSTAN_BRIDGE_SELF_CONTAINED "Publish the bridge with its own .NET runtime" ON)

find_package(Qt6 6.5 REQUIRED COMPONENTS Core Gui Qml Quick QuickControls2 OpenGL Concurrent)
qt_standard_project_setup(REQUIRES 6.5)
if(QT_KNOWN_POLICY_QTP0004)
    qt_policy(SET QTP0004 NEW)
endif()

# ---- libmpv (video playback) ------------------------------------------------
# Linux/macOS: found through pkg-config. Windows: pass -DMPV_DIR=<folder with include/ and libmpv.dll.a or mpv.lib>.
set(MPV_DIR "" CACHE PATH "Folder containing libmpv headers (include/mpv) and import library")
set(MPV_RUNTIME "" CACHE FILEPATH "Windows: libmpv-2.dll to install next to Dustan.exe")
if(MPV_DIR)
    find_path(MPV_INCLUDE_DIR mpv/client.h PATHS "${MPV_DIR}" "${MPV_DIR}/include" NO_DEFAULT_PATH REQUIRED)
    find_library(MPV_LIBRARY NAMES mpv libmpv libmpv.dll.a PATHS "${MPV_DIR}" "${MPV_DIR}/lib" NO_DEFAULT_PATH REQUIRED)
    add_library(mpv::mpv UNKNOWN IMPORTED)
    set_target_properties(mpv::mpv PROPERTIES IMPORTED_LOCATION "${MPV_LIBRARY}" INTERFACE_INCLUDE_DIRECTORIES "${MPV_INCLUDE_DIR}")
else()
    find_package(PkgConfig REQUIRED)
    pkg_check_modules(MPV REQUIRED IMPORTED_TARGET mpv>=2.0)
    add_library(mpv::mpv ALIAS PkgConfig::MPV)
endif()

qt_add_executable(dustan
    src/main.cpp
    src/BridgeClient.cpp src/BridgeClient.h
    src/JsonListModel.cpp src/JsonListModel.h
    src/MpvPlayer.cpp src/MpvPlayer.h
    src/VideoThumbnailProvider.cpp src/VideoThumbnailProvider.h
    src/Platform.cpp src/Platform.h
)

set_source_files_properties(qml/Theme.qml PROPERTIES QT_QML_SINGLETON_TYPE TRUE)

qt_add_qml_module(dustan
    URI Dustan
    VERSION 1.0
    # Keep the module's build folder clear of the Dustan executable.
    OUTPUT_DIRECTORY ${CMAKE_CURRENT_BINARY_DIR}/qml/Dustan
    QML_FILES
        qml/Main.qml
        qml/Theme.qml
        qml/Tour.qml
        qml/Verify.qml
        qml/components/Icon.qml
        qml/components/IconButton.qml
        qml/components/PillButton.qml
        qml/components/Artwork.qml
        qml/components/VideoThumb.qml
        qml/components/SearchField.qml
        qml/components/Segmented.qml
        qml/components/SeekBar.qml
        qml/components/ProgressLine.qml
        qml/components/NavRail.qml
        qml/components/MiniPlayer.qml
        qml/components/PageHeader.qml
        qml/components/EmptyState.qml
        qml/components/Field.qml
        qml/components/Toggle.qml
        qml/components/PopupMenu.qml
        qml/components/Sheet.qml
        qml/components/Toast.qml
        qml/components/Spinner.qml
        qml/components/EqualizerBars.qml
        qml/components/TrackRow.qml
        qml/components/AlbumCard.qml
        qml/components/VideoCard.qml
        qml/components/SectionTitle.qml
        qml/components/ThinScrollBar.qml
        qml/pages/MusicPage.qml
        qml/pages/AlbumsView.qml
        qml/pages/ArtistsView.qml
        qml/pages/SongsView.qml
        qml/pages/AlbumPage.qml
        qml/pages/ArtistPage.qml
        qml/pages/NowPlayingPage.qml
        qml/pages/VideosPage.qml
        qml/pages/ShowPage.qml
        qml/pages/TvGuidePage.qml
        qml/pages/CreateChannelSheet.qml
        qml/pages/EditTagsSheet.qml
        qml/pages/SettingsPage.qml
        qml/player/VideoPlayer.qml
    RESOURCES
        fonts/Inter-Regular.ttf
        fonts/Inter-Medium.ttf
        fonts/Inter-SemiBold.ttf
        fonts/Inter-Bold.ttf
        packaging/dustan.svg
)

target_include_directories(dustan PRIVATE src)

target_compile_definitions(dustan PRIVATE
    DUSTAN_VERSION="${DUSTAN_VERSION}"
    DUSTAN_SOURCE_DIR="${CMAKE_CURRENT_SOURCE_DIR}"
    $<$<CONFIG:Release>:QT_NO_DEBUG_OUTPUT>)

target_link_libraries(dustan PRIVATE
    Qt6::Core Qt6::Gui Qt6::Qml Qt6::Quick Qt6::QuickControls2 Qt6::OpenGL Qt6::Concurrent
    mpv::mpv)

set_target_properties(dustan PROPERTIES
    OUTPUT_NAME Dustan
    WIN32_EXECUTABLE TRUE
    MACOSX_BUNDLE TRUE
    MACOSX_BUNDLE_GUI_IDENTIFIER app.dustan.player
    MACOSX_BUNDLE_BUNDLE_NAME Dustan
    MACOSX_BUNDLE_BUNDLE_VERSION ${DUSTAN_VERSION}
    MACOSX_BUNDLE_SHORT_VERSION_STRING ${DUSTAN_VERSION})

if(WIN32)
    target_sources(dustan PRIVATE packaging/dustan.rc)
endif()

# ---- .NET bridge -------------------------------------------------------------
if(APPLE)
    set(DUSTAN_BRIDGE_DIR "$<TARGET_BUNDLE_CONTENT_DIR:dustan>/Resources/bridge")
else()
    set(DUSTAN_BRIDGE_DIR "$<TARGET_FILE_DIR:dustan>/bridge")
endif()

if(DUSTAN_BUILD_BRIDGE)
    find_program(DOTNET_EXECUTABLE dotnet REQUIRED)
    if(NOT DUSTAN_RID)
        string(TOLOWER "${CMAKE_SYSTEM_PROCESSOR}" _cpu)
        if(CMAKE_OSX_ARCHITECTURES)
            set(_cpu "${CMAKE_OSX_ARCHITECTURES}")
        endif()
        if(_cpu MATCHES "arm64|aarch64")
            set(_arch arm64)
        else()
            set(_arch x64)
        endif()
        if(WIN32)
            set(DUSTAN_RID win-${_arch})
        elseif(APPLE)
            set(DUSTAN_RID osx-${_arch})
        else()
            set(DUSTAN_RID linux-${_arch})
        endif()
    endif()
    if(DUSTAN_BRIDGE_SELF_CONTAINED)
        set(_sc --self-contained true)
    else()
        set(_sc --self-contained false)
    endif()
    file(GLOB_RECURSE _bridge_sources CONFIGURE_DEPENDS
        "${CMAKE_CURRENT_SOURCE_DIR}/../Dustan.Bridge/*.cs" "${CMAKE_CURRENT_SOURCE_DIR}/../Dustan.Bridge/*.csproj"
        "${CMAKE_CURRENT_SOURCE_DIR}/../Dustan.Core/*.cs" "${CMAKE_CURRENT_SOURCE_DIR}/../Dustan.Core/*.csproj")
    list(FILTER _bridge_sources EXCLUDE REGEX "/(bin|obj)/")
    set(_stamp "${CMAKE_CURRENT_BINARY_DIR}/bridge-${DUSTAN_RID}.stamp")
    add_custom_command(OUTPUT "${_stamp}"
        COMMAND "${DOTNET_EXECUTABLE}" publish "${CMAKE_CURRENT_SOURCE_DIR}/../Dustan.Bridge/Dustan.Bridge.csproj"
                -c Release -r ${DUSTAN_RID} ${_sc} -p:Version=${DUSTAN_VERSION} --nologo -v quiet -o "${CMAKE_CURRENT_BINARY_DIR}/bridge-publish"
        COMMAND "${CMAKE_COMMAND}" -E touch "${_stamp}"
        DEPENDS ${_bridge_sources}
        COMMENT "Publishing Dustan.Bridge (${DUSTAN_RID})"
        VERBATIM)
    add_custom_target(dustan_bridge DEPENDS "${_stamp}")
    add_dependencies(dustan dustan_bridge)
    add_custom_command(TARGET dustan POST_BUILD
        COMMAND "${CMAKE_COMMAND}" -E copy_directory "${CMAKE_CURRENT_BINARY_DIR}/bridge-publish" "${DUSTAN_BRIDGE_DIR}"
        VERBATIM)
endif()

# ---- install / deploy --------------------------------------------------------
# DUSTAN_DEPLOY_QT bundles the Qt runtime (windeployqt / macdeployqt / Qt's Linux deployment)
# for self-contained release archives. Local Linux installs use the system Qt instead.
#   Bundled:     <prefix>/bin/{Dustan[.exe],bridge/} + Qt libraries, plugins and QML (macOS: Dustan.app)
#   Linux local: <prefix>/lib/dustan/{Dustan,bridge/} + <prefix>/bin/dustan launcher + desktop entry
option(DUSTAN_DEPLOY_QT "Bundle the Qt runtime into the install tree" OFF)
if(WIN32 OR APPLE)
    set(DUSTAN_DEPLOY_QT ON CACHE BOOL "" FORCE)
endif()

include(GNUInstallDirs)
if(UNIX AND NOT APPLE AND NOT DUSTAN_DEPLOY_QT)
    set(DUSTAN_APP_DIR "lib/dustan")   # fixed path: the bin/dustan launcher refers to it
else()
    set(DUSTAN_APP_DIR "${CMAKE_INSTALL_BINDIR}")
endif()
install(TARGETS dustan
    BUNDLE DESTINATION .
    RUNTIME DESTINATION ${DUSTAN_APP_DIR})

if(DUSTAN_BUILD_BRIDGE)
    if(APPLE)
        install(DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/bridge-publish/" DESTINATION "Dustan.app/Contents/Resources/bridge" USE_SOURCE_PERMISSIONS)
    else()
        install(DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/bridge-publish/" DESTINATION "${DUSTAN_APP_DIR}/bridge" USE_SOURCE_PERMISSIONS)
    endif()
endif()

if(UNIX AND NOT APPLE AND NOT DUSTAN_DEPLOY_QT)
    install(PROGRAMS packaging/dustan-launcher.sh DESTINATION ${CMAKE_INSTALL_BINDIR} RENAME dustan)
    install(FILES packaging/dustan.desktop DESTINATION ${CMAKE_INSTALL_DATADIR}/applications)
    install(FILES packaging/dustan.svg DESTINATION ${CMAKE_INSTALL_DATADIR}/icons/hicolor/scalable/apps)
endif()

if(WIN32 AND MPV_RUNTIME)
    install(FILES "${MPV_RUNTIME}" DESTINATION ${DUSTAN_APP_DIR})
endif()

if(DUSTAN_DEPLOY_QT AND UNIX AND NOT APPLE)
    # libmpv is a system dependency on Linux (and some distributions link its own
    # dependencies by absolute path, which the dependency scan rejects), so skip it.
    # Qt expands variables in CONTENT at configure time, so keep the deploy-time steps in a file.
    file(WRITE "${CMAKE_CURRENT_BINARY_DIR}/dustan-linux-deploy.cmake" [=[
qt_deploy_qml_imports(TARGET dustan PLUGINS_FOUND _plugins)
qt_deploy_runtime_dependencies(
    EXECUTABLE "${QT_DEPLOY_BIN_DIR}/Dustan"
    ADDITIONAL_MODULES ${_plugins}
    GENERATE_QT_CONF
    PRE_EXCLUDE_REGEXES "^libmpv[.]so"
)
]=])
    qt_generate_deploy_script(TARGET dustan OUTPUT_SCRIPT _deploy_script
        CONTENT "include(\"${CMAKE_CURRENT_BINARY_DIR}/dustan-linux-deploy.cmake\")")
    install(SCRIPT ${_deploy_script})
elseif(DUSTAN_DEPLOY_QT)
    qt_generate_deploy_qml_app_script(TARGET dustan OUTPUT_SCRIPT _deploy_script)
    install(SCRIPT ${_deploy_script})
endif()
