// Utility functions
def shortenURL(longURL) {
    def apiResponse = sh(script: """curl -s -X POST -H "Content-Type: application/json" -d '{"url": "${longURL}"}' https://6bk2ce6dpk.execute-api.eu-west-2.amazonaws.com/shorten""", returnStdout: true).trim()
    def jsonResponse = readJSON text: apiResponse
    return jsonResponse.ShortUrl
}

def scheduleDeleteURL(shortURL) {
    // Schedule a DELETE request 12 hours later
    sh """
    ( echo "curl -s -X DELETE ${shortURL}" | at now + 12 hours ) </dev/null >/dev/null 2>&1
    """
}

pipeline {
    agent { label env.NODE_NAME }

    environment {
        UNITY_CREDS = credentials('UNITY_CREDS')
        UNITY_SERIAL = credentials('UNITY_SERIAL')
    }

    stages {
        stage('Set Parameters') {
            steps {
                script {
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        sh 'python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" --color "info" --job_name $JOB_NAME --build-type "$BUILD_TYPE" --build-purpose "$BUILD_PURPOSE" --description "$DESCRIPTION"'
                        def attachments = readJSON file: 'slack_message.json'
                        def slackResponse = slackSend(channel: env.SLACK_BUILDS_CHANNEL_NAME, attachments: attachments)
                        def slackResponseDetailed = slackSend(channel: "build-automation-detailed", attachments: attachments)
                        env.THREAD_ID = slackResponseDetailed.threadId
                        env.CHANNEL_ID = slackResponseDetailed.channelId
                        env.FIRST_MESSAGE_TS = slackResponseDetailed.ts
                        env.THREAD_ID_NON_DETAILED = slackResponse.threadId
                        env.CHANNEL_ID_NON_DETAILED = slackResponse.channelId
                        env.FIRST_MESSAGE_TS_NON_DETAILED = slackResponse.ts
                    }
                    env.S3_ICONS_FOLDER = 'Icons/'
                }
            }
        }
        
        stage('Checkout SCM') {
            steps {
                script {
                    def repoUrl = params.GIT_REPOSITORY_URL.trim()
                    if (!repoUrl) {
                        error "GIT_REPOSITORY_URL parameter must be provided."
                    }
                    
                    if (fileExists('.git')) {
                        echo ".git directory found. Fetching updates and discarding local changes..."
                        sh "git fetch --all --tags --force --prune"
                        sh "git reset --hard HEAD"
                        // Clean untracked files but preserve ignored files (e.g. Library)
                        sh "git clean -fd"
                    } else {
                        echo "No .git directory found. Cloning fresh repository..."
                        deleteDir()
                        sh "git clone ${repoUrl} ."
                    }
                    
                    // Use the partial COMMIT_HASH to get the full commit hash.
                    def partialCommit = params.COMMIT_HASH.trim()
                    if (!partialCommit) {
                        error "COMMIT_HASH parameter must be provided."
                    }
                    def matchingCommit = sh(
                        script: "git log --pretty=format:%H --all | grep -m 1 '^${partialCommit}'",
                        returnStdout: true
                    ).trim()
                    if (!matchingCommit) {
                        error "No commit found matching partial hash: ${partialCommit}"
                    }
                    echo "Found matching commit: ${matchingCommit}"
                    
                    // Checkout the matching commit (detached HEAD)
                    sh "git checkout ${matchingCommit}"
                    
                    // Capture commit comment (message)
                    env.CHANGESET_COMMENT = sh(script: "git log -1 --pretty=%B ${matchingCommit}", returnStdout: true).trim()
                    echo "Detected commit comment: ${env.CHANGESET_COMMENT}"
                    
                    env.CHANGESET = matchingCommit
                    def projectSettingsPath = 'ProjectSettings/ProjectSettings.asset'
                    def bundleVersion = sh(script: "grep 'bundleVersion:' $projectSettingsPath | awk '{print \$2}'", returnStdout: true).trim()
                    env.BUNDLE_VERSION = bundleVersion
                    env.ARCHIVE_NAME = "${bundleVersion}_${matchingCommit}_${BUILD_NUMBER}"
                    // Delete all spaces in PROJECT_NAME
                    env.S3FOLDER = "${PROJECT_NAME.replaceAll(' ', '')}/${PLATFORM}/${bundleVersion}_${matchingCommit}_${BUILD_NUMBER}/"
                    
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${matchingCommit}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --job_name "$JOB_NAME"
                        """
                                                def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS)
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    
                    if (DELETE_LIBRARY == 'true') {
                        sh 'rm -rf Library'
                    } else if (CLEAN_BUILD == 'true') {
                        sh 'rm -rf Library/Bee'
                        sh "rm -rf Library/BurstCache"
                    }
                }
            }
        }
        
        stage('Extract Unity Version') {
            steps {
                script {
                    def unityVersion = sh(
                        script: "grep '^m_EditorVersion:' ${PROJECT_SETTINGS_PATH} | cut -d ' ' -f 2",
                        returnStdout: true
                    ).trim()
                    env.UNITY_VERSION = unityVersion
                    echo "Detected Unity Version: ${env.UNITY_VERSION}"
                }
            }
        }
        
        stage('Set CI/CD Symbol') {
            steps {
                script {
                    sh 'python3 $AUTOMATION_SCRIPTS_FOLDER/set_cicd_symbol.py'
                }
            }
        }
        
        stage('Activate Licence') {
            steps {
                script {
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":key:  Activate Licence")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 1 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    lock('UNITY_LICENSE_LOCK') {
                        if (fileExists('/tmp/current_builds.txt')) {
                            echo 'File exists!'
                            def fileSize = sh(script: 'stat -f %z /tmp/current_builds.txt', returnStdout: true).trim()
                            if (fileSize == '0') {
                                echo 'The file is empty! Writing job name and build number...'
                                sh "echo '${JOB_NAME} - ${BUILD_NUMBER}' > /tmp/current_builds.txt"
                                echo 'Activating licence...'
                                sh '''
                                /Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -quit -username $UNITY_CREDS_USR -password $UNITY_CREDS_PSW -serial $UNITY_SERIAL -batchMode -logFile - -silent-crashes -noUpm
                                '''
                            } else {
                                echo 'The file is not empty! Appending job name and build number...'
                                sh "echo '${JOB_NAME} - ${BUILD_NUMBER}' >> /tmp/current_builds.txt"
                                echo 'Licence is already activated'
                            }
                        } else {
                            echo '/tmp/current_builds.txt does not exist! Creating it and writing job name and build number...'
                            sh "echo '${JOB_NAME} - ${BUILD_NUMBER}' > /tmp/current_builds.txt"
                            sh '''
                            /Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -quit -username $UNITY_CREDS_USR -password $UNITY_CREDS_USR -serial $UNITY_SERIAL -batchMode -logFile - -silent-crashes -noUpm
                            '''
                        }
                    }
                }
            }
        }
        
        stage('Import Build Automater Package') {
            steps {
                script {
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":syringe:  Import Build Automater Package")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 2 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    sh '''
                    /Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -quit -importPackage $AUTOMATION_SCRIPTS_FOLDER/$AUTOMATER_UNITY_PACKAGE_NAME -batchMode -projectPath . -logFile - -stackTraceLogType Full -silent-crashes -buildTarget $PLATFORM
                    '''
                }
            }
        }
        
        stage('Import Assets') {
            steps {
                script {
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":file_cabinet:  Import Assets")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 3 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """                        
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    sh '''
                    /Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -quit -executeMethod PrebuildSettings.ImportAssets -buildTarget $PLATFORM -buildEnvironmentType $BUILD_TYPE -localServerType $LOCAL_SERVER_TYPE -buildPurposeType $BUILD_PURPOSE -forceEnableSrDebugger $FORCE_ENABLE_SRDEBUGGER -forceDisableObfuscator $FORCE_DISABLE_OBFUSCATOR -scriptingImplementation $SCRIPTING_IMPLEMENTATION -productionStaging $PROD_STAGING -isAddressableBuild $IS_ADDRESSABLE_BUILD -forceDevelopmentBuild $FORCE_DEVELOPMENT_BUILD -cleanBuild $CLEAN_BUILD -vehicleRebuildOption $VEHICLE_REBUILD_OPTION -batchMode -projectPath . -logFile - -stackTraceLogType Full -silent-crashes
                    '''
                }
            }
        }
        
        // ------------------ Android-specific stages ------------------
        stage('Setup Fastlane Android') {
            when {
                beforeAgent true
                expression { return env.PLATFORM == 'Android' }
            }
            steps {
                script {
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":motorway:  Setup Fastlane")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 4 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """             
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    sh "python3 $AUTOMATION_SCRIPTS_FOLDER/generate_internal_test_fastfile.py --bundle-id $BUNDLE_ID --api-key-path $GOOGLE_PLAY_API_KEY"
                }
            }
        }
        
        stage('Get Version Code') {
            when {
                beforeAgent true
                expression { return env.PLATFORM == 'Android' && BUILD_TYPE == 'Production' && BUILD_PURPOSE != 'Testing' }
            }
            steps {
                script {
                    
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":screwdriver:  Get Version Code")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 5 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """                                     
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    
                    lock('GOOGLE_PLAY_API_LOCK') {
                        sh 'fastlane android increment_version_code'
                    }
                }
            }
        }
        
        stage('Fix ADB (Android)') {
          when { expression { return env.PLATFORM == 'Android' } }
          steps {
            sh '''
              ADB="/Applications/Unity/Hub/Editor/$UNITY_VERSION/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
              "$ADB" kill-server || true
              # kill any other adb that might be listening on 5037
              lsof -ti tcp:5037 | xargs -r kill -9 || true
              "$ADB" start-server
              "$ADB" version
              "$ADB" devices
            '''
          }
        }

        stage('On Pre Build') {
            steps {
                script {
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":screwdriver:  On Pre Build")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 6 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """                                     
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    sh '''
                    /Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -executeMethod PrebuildSettings.OnPreBuild -buildTarget $PLATFORM -buildEnvironmentType $BUILD_TYPE -localServerType $LOCAL_SERVER_TYPE -buildPurposeType $BUILD_PURPOSE -forceEnableSrDebugger $FORCE_ENABLE_SRDEBUGGER -forceDisableObfuscator $FORCE_DISABLE_OBFUSCATOR -scriptingImplementation $SCRIPTING_IMPLEMENTATION -productionStaging $PROD_STAGING -isAddressableBuild $IS_ADDRESSABLE_BUILD -forceDevelopmentBuild $FORCE_DEVELOPMENT_BUILD -cleanBuild $CLEAN_BUILD -vehicleRebuildOption $VEHICLE_REBUILD_OPTION -batchMode -projectPath . -logFile - -stackTraceLogType Full -silent-crashes
                    '''
                }
            }
        }
        
        stage('Build From Unity') {
            steps {
                script {
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":hammer_and_wrench:  Build From Unity")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 8 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """             
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    sh 'rm -rf Builds'
                    sh '''
                    /Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -quit -executeMethod Builder.Build -buildTarget $PLATFORM -buildEnvironmentType $BUILD_TYPE -localServerType $LOCAL_SERVER_TYPE -buildPurposeType $BUILD_PURPOSE -forceEnableSrDebugger $FORCE_ENABLE_SRDEBUGGER -forceDisableObfuscator $FORCE_DISABLE_OBFUSCATOR -scriptingImplementation $SCRIPTING_IMPLEMENTATION -productionStaging $PROD_STAGING -isAddressableBuild $IS_ADDRESSABLE_BUILD -forceDevelopmentBuild $FORCE_DEVELOPMENT_BUILD -cleanBuild $CLEAN_BUILD -vehicleRebuildOption $VEHICLE_REBUILD_OPTION -batchMode -projectPath . -logFile - -stackTraceLogType Full -silent-crashes
                    '''
                }
            }
        }
        
        stage('Upload Archive') {
            steps {
                script {
                    // Notify Slack if active
                    if (SLACK_MESSAGES_ACTIVE == 'true') {
                        slackSend(channel: env.THREAD_ID, message: ":arrow_up:  Upload Archives")
                        sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                            --title ":zap: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                            --description "${params.DESCRIPTION}" \
                            --commit "${env.CHANGESET}" \
                            --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                            --build-type "$BUILD_TYPE" \
                            --build-purpose "$BUILD_PURPOSE" \
                            --color "info" \
                            --current-step 9 \
                            --total-steps 9 \
                            --job_name "$JOB_NAME"
                        """             
                        def attachments = readJSON file: 'slack_message.json'
                        slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                    }
                    
                    // Determine which APK to upload
                    def apkFile = ''
                    if ((FORCE_DEVELOPMENT_BUILD == 'true' || BUILD_TYPE == 'Development') || (BUILD_TYPE == 'Production' && BUILD_PURPOSE == 'Testing')) {
                        apkFile = 'launcher.apk'
                    }
                    
                    if (apkFile) {
                        // Upload APK to S3 and schedule its deletion
                        sh "aws s3 cp Builds/Android/${apkFile} 's3://${BUCKET_NAME}/${S3FOLDER}${ARCHIVE_NAME}.apk'"
                        def presignedApkURL = sh(script: "aws s3 presign 's3://${S3FOLDER}${ARCHIVE_NAME}.apk' --expires-in ${URL_EXPIRATION} --endpoint-url https://${BUCKET_NAME}.s3-accelerate.amazonaws.com", returnStdout: true).trim()
                        env.PRESIGNED_URL = shortenURL(presignedApkURL)
                        echo "APK uploaded! Access Shortened Presigned URL: $PRESIGNED_URL"
                        sh "( echo \"aws s3 rm s3://${BUCKET_NAME}/${S3FOLDER} --recursive\" | at now + 12 hours ) </dev/null >/dev/null 2>&1"
                        scheduleDeleteURL(env.PRESIGNED_URL)
                    } else {
                        // Handle non-APK uploads
                        if (INTERNAL_APP_SHARING == 'true') {
                            sh 'fastlane android upload_to_internal_app_sharing'
                            def appSharingURL = readFile('fastlane/appSharingURL.txt').trim()
                            env.PRESIGNED_HTML_URL = appSharingURL
                            echo "AAB uploaded! Access URL: $PRESIGNED_HTML_URL"
                        } else {
                            sh 'fastlane android upload_to_internal_test'
							
							// ===== [SLACK-RELEASE-NAME] verbose diagnostics for the Play release-name Slack message =====
							echo "[SLACK-RELEASE-NAME] ---- BEGIN diagnostics ----"
							sh 'echo "[SLACK-RELEASE-NAME] pwd=$(pwd)"; echo "[SLACK-RELEASE-NAME] candidate file listing:"; ls -la playReleaseName.txt fastlane/playReleaseName.txt 2>&1 || true'
							def releaseNameInFastlane = fileExists('fastlane/playReleaseName.txt')
							def releaseNameInRoot = fileExists('playReleaseName.txt')
							echo "[SLACK-RELEASE-NAME] fileExists fastlane/playReleaseName.txt = ${releaseNameInFastlane}"
							echo "[SLACK-RELEASE-NAME] fileExists playReleaseName.txt (root)   = ${releaseNameInRoot}  (root copy is only a hint; this case reads from fastlane/)"

							if (releaseNameInFastlane) {
								env.PLAY_RELEASE_NAME = readFile('fastlane/playReleaseName.txt').trim()
							} else {
								echo "[SLACK-RELEASE-NAME] ERROR: fastlane/playReleaseName.txt not found - PLAY_RELEASE_NAME will be empty and the Slack message will be skipped"
								env.PLAY_RELEASE_NAME = ''
							}
							echo "[SLACK-RELEASE-NAME] Final Play release name: '${env.PLAY_RELEASE_NAME}'"

							def isProductionOrProductionStaging =
								BUILD_TYPE == 'Production' ||
								BUILD_TYPE == 'Production Staging' ||
								(BUILD_TYPE == 'Production' && PROD_STAGING == 'true')

							// Log every guard of the Slack-message condition so we can see which one blocks it
							echo "[SLACK-RELEASE-NAME] BUILD_TYPE='${BUILD_TYPE}' BUILD_PURPOSE='${BUILD_PURPOSE}' PROD_STAGING='${PROD_STAGING}' INTERNAL_APP_SHARING='${INTERNAL_APP_SHARING}' SLACK_MESSAGES_ACTIVE='${SLACK_MESSAGES_ACTIVE}'"
							echo "[SLACK-RELEASE-NAME] THREAD_ID='${env.THREAD_ID}'"
							echo "[SLACK-RELEASE-NAME] guard isProductionOrProductionStaging = ${isProductionOrProductionStaging}"
							echo "[SLACK-RELEASE-NAME] guard BUILD_PURPOSE != 'Testing'       = ${BUILD_PURPOSE != 'Testing'}"
							echo "[SLACK-RELEASE-NAME] guard INTERNAL_APP_SHARING != 'true'    = ${INTERNAL_APP_SHARING != 'true'}"
							echo "[SLACK-RELEASE-NAME] guard SLACK_MESSAGES_ACTIVE == 'true'   = ${SLACK_MESSAGES_ACTIVE == 'true'}"
							echo "[SLACK-RELEASE-NAME] guard PLAY_RELEASE_NAME not empty       = ${env.PLAY_RELEASE_NAME?.trim() ? true : false}"

							if (
								isProductionOrProductionStaging &&
								BUILD_PURPOSE != 'Testing' &&
								INTERNAL_APP_SHARING != 'true' &&
								SLACK_MESSAGES_ACTIVE == 'true' &&
								env.PLAY_RELEASE_NAME?.trim()
							) {
								echo "[SLACK-RELEASE-NAME] All guards passed -> sending Slack message to THREAD_ID='${env.THREAD_ID}'"
								slackSend(
									channel: env.THREAD_ID_NON_DETAILED,
									message: """:google-play: *Google Play upload completed successfully!*
							:label: *Release name used:* `${env.PLAY_RELEASE_NAME}`
							:rocket: This build is now attached to the Play release above."""
								)
								echo "[SLACK-RELEASE-NAME] Slack message sent."
							} else {
								echo "[SLACK-RELEASE-NAME] Slack message SKIPPED - one or more guards above evaluated false."
							}
							echo "[SLACK-RELEASE-NAME] ---- END diagnostics ----"
                        }
                    }
                    
                    // Additional steps if BUILD_PURPOSE is Testing
                    if (BUILD_PURPOSE == 'Testing') {
                        def presignedFullSizeIconURL = sh(script: "aws s3 presign 's3://${S3_ICONS_FOLDER}${FULL_SIZE_ICON}' --expires-in ${URL_EXPIRATION} --endpoint-url https://${BUCKET_NAME}.s3-accelerate.amazonaws.com", returnStdout: true).trim()
                        def shortenedFullSizeIconURL = shortenURL(presignedFullSizeIconURL)
                        echo "Access Presigned Full Size Icon URL: ${shortenedFullSizeIconURL}"
                        env.SHORTENED_FULL_SIZE_ICON_URL = shortenedFullSizeIconURL
                        scheduleDeleteURL(env.SHORTENED_FULL_SIZE_ICON_URL)
                        
                        dir('Builds/Android') {
                            sh 'python3 $AUTOMATION_SCRIPTS_FOLDER/generate_adhoc_html.py --url $PRESIGNED_URL --game-name "$PROJECT_NAME" --description "$DESCRIPTION" --commit $CHANGESET --build-type $BUILD_TYPE --build-purpose $BUILD_PURPOSE --icon-url $SHORTENED_FULL_SIZE_ICON_URL --platform Android'
                        }
                        
                        def htmlFile = sh(script: "find Builds/Android -name '*.html' -print -quit", returnStdout: true).trim()
                        if (htmlFile) {
                            def htmlName = htmlFile.split('/').last()
                            sh "aws s3 cp ${htmlFile} 's3://${BUCKET_NAME}/${S3FOLDER}${htmlName}' --content-type 'text/html'"
                            def presignedHtmlURL = sh(script: "aws s3 presign 's3://${S3FOLDER}${htmlName}' --expires-in ${URL_EXPIRATION} --endpoint-url https://${BUCKET_NAME}.s3-accelerate.amazonaws.com", returnStdout: true).trim()
                            def shortenedHtmlURL = shortenURL(presignedHtmlURL)
                            echo "${htmlName} uploaded! Access Presigned URL: ${shortenedHtmlURL}"
                            env.PRESIGNED_HTML_URL = shortenedHtmlURL
                            scheduleDeleteURL(env.PRESIGNED_HTML_URL)
                        } else {
                            error 'No HTML file detected in the Android directory.'
                        }
                    }
                }
            }
        }
    
        stage('Upload symbols To Firebase') {
            when {
                beforeAgent true
                expression { return env.PLATFORM == 'Android' && BUILD_TYPE == 'Production' && BUILD_PURPOSE != 'Testing' && INTERNAL_APP_SHARING != 'true' }
                }
            steps {
                script {
                       def symbols = findFiles(glob: 'Builds/Android/*.symbols.zip')
                        if (symbols && symbols.length > 0) {
                            def symbolFile = symbols[0].path
                            echo "Found symbols file: ${symbolFile}"
                            sh "firebase crashlytics:symbols:upload --app=1:423412055983:android:3badb43c1aeb9667da395a ${symbolFile}"
                            echo "Symbols uplaoded to firebase crashlytics"
                        } else {
                            echo "No symbols file found in Builds/Android/"
                        }
                }
            }
        }
        
    }
    
    post {
        always {
            script {
                lock('UNITY_LICENSE_LOCK') {
                    if (fileExists('/tmp/current_builds.txt')) {
                        echo 'File exists! Removing line with job name and build number...'
                        sh "sed -i '' '/${JOB_NAME} - ${BUILD_NUMBER}/d' /tmp/current_builds.txt"
                        def fileSize = sh(script: 'stat -f %z /tmp/current_builds.txt', returnStdout: true).trim()
                        if (fileSize == '0') {
                            echo 'The file is now empty after removal!'
                            //sh '''
                            ///Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -quit -batchmode -returnlicense -username $UNITY_CREDS_USR -password $UNITY_CREDS_PSW
                            //'''
                            sh '''
                            /Applications/Unity/Hub/Editor/$UNITY_VERSION/Unity.app/Contents/MacOS/Unity -quit -batchmode -returnlicense -username $UNITY_CREDS_USR -password $UNITY_CREDS_PSW > unity_returnlicense.log 2>&1
                            cat unity_returnlicense.log
                            '''
                        } else {
                            echo 'The file still contains build lines after removal.'
                        }
                    } else {
                        echo '/tmp/current_builds.txt does not exist! No action taken.'
                    }
                }
            }
        }
        success {
            script {
               if (SLACK_MESSAGES_ACTIVE == 'true') {
                    def slackMessageCmd = "python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py" +
                        " --title \":tada: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER\"" +
                        " --description \"${params.DESCRIPTION}\"" +
                        " --commit \"${env.CHANGESET}\"" +
                        " --commit-comment \"${env.CHANGESET_COMMENT.replace('"', '\\"')}\"" +
                        " --build-type \"$BUILD_TYPE\"" +
                        " --build-purpose \"$BUILD_PURPOSE\"" +
                        " --color \"good\"" +
                        " --job_name \"$JOB_NAME\""
                
                    if (env.PRESIGNED_HTML_URL) {
                        slackMessageCmd += " --download-url \"${env.PRESIGNED_HTML_URL}\""
                    }
                
                    echo "Running Slack message command: ${slackMessageCmd}"
                    sh slackMessageCmd
                
                    def attachments = readJSON file: 'slack_message.json'
                    slackSend(channel: env.THREAD_ID, replyBroadcast: true, attachments: attachments)
                    slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                }
            }
        }
        failure {
            script {
                if (SLACK_MESSAGES_ACTIVE == 'true') {
                    sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                        --title ":x: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                        --description "${params.DESCRIPTION}" \
                        --commit "${env.CHANGESET}" \
                        --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                        --build-type "$BUILD_TYPE" \
                        --build-purpose "$BUILD_PURPOSE" \
                        --color "danger" \
                        --job_name "$JOB_NAME"
                    """
                    def attachments = readJSON file: 'slack_message.json'
                    def slackResponseDetailed = slackSend(channel: env.THREAD_ID, replyBroadcast: true, attachments: attachments)
                    def slackResponse = slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)

                    sh """python3 $AUTOMATION_SCRIPTS_FOLDER/find_build_error.py \
                        --job-name "$JOB_NAME" \
                        --build-no "$BUILD_NUMBER"
                    """

                    if (fileExists('possible_errors.txt')) {
                        def possibleErrors = readFile('possible_errors.txt').trim()
                        slackSend(channel: env.THREAD_ID, message: possibleErrors)
                        slackSend(channel: slackResponse.threadId, message: possibleErrors)
                    }
                }
            }
        }

        aborted {
            script {
                if (SLACK_MESSAGES_ACTIVE == 'true') {
                    sh """python3 $AUTOMATION_SCRIPTS_FOLDER/generate_slack_message.py \
                        --title ":no_entry_sign: $PROJECT_NAME - $PLATFORM #$BUILD_NUMBER" \
                        --description "${params.DESCRIPTION}" \
                        --commit "${env.CHANGESET}" \
                        --commit-comment "${env.CHANGESET_COMMENT.replace('"', '\\"')}" \
                        --build-type "$BUILD_TYPE" \
                        --build-purpose "$BUILD_PURPOSE" \
                        --color "warning" \
                        --job_name "$JOB_NAME"
                    """
                    def attachments = readJSON file: 'slack_message.json'
                    slackSend(channel: env.THREAD_ID, replyBroadcast: true, attachments: attachments)
                    slackSend(channel: env.CHANNEL_ID_NON_DETAILED, attachments: attachments, timestamp: env.FIRST_MESSAGE_TS_NON_DETAILED)
                }
            }
        }
    }
}
