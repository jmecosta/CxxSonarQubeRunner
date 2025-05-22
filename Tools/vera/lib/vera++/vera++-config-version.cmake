# This is a basic version file for the Config-mode of find_package().
# It is used by write_basic_package_version_file() as input file for configure_file()
# to create a version-file which can be installed along a config.cmake file.
#
# The created file sets PACKAGE_VERSION_EXACT if the current version string and
# the requested version string are exactly the same and it sets
# PACKAGE_VERSION_COMPATIBLE if the current version is >= requested version.
# The variable CVF_VERSION must be set before calling configure_file().

set(PACKAGE_VERSION "1.3.0")

if("" VERSION_LESS "" )
  set(PACKAGE_VERSION_COMPATIBLE FALSE)
else()
  set(PACKAGE_VERSION_COMPATIBLE TRUE)
  if( "" STREQUAL "")
    set(PACKAGE_VERSION_EXACT TRUE)
  endif()
endif()
