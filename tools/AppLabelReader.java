package com.ahui3c.androidadbtools;

import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Looper;
import android.util.Base64;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;

public final class AppLabelReader {
    private static Context systemContext() throws Exception {
        Class<?> activityThreadClass = Class.forName("android.app.ActivityThread");
        Method systemMain = activityThreadClass.getDeclaredMethod("systemMain");
        systemMain.setAccessible(true);
        Object activityThread = systemMain.invoke(null);
        Method getSystemContext = activityThreadClass.getDeclaredMethod("getSystemContext");
        getSystemContext.setAccessible(true);
        return (Context)getSystemContext.invoke(activityThread);
    }

    private static boolean isUninstallBlocked(Context context, String packageName) {
        try {
            Class<?> appGlobals = Class.forName("android.app.AppGlobals");
            Object packageManager = appGlobals.getDeclaredMethod("getPackageManager").invoke(null);
            for (Method method : packageManager.getClass().getMethods()) {
                if (method.getName().equals("getBlockUninstallForUser") && method.getParameterTypes().length == 2) {
                    method.setAccessible(true);
                    Object value = method.invoke(packageManager, packageName, 0);
                    if (value instanceof Boolean && ((Boolean)value)) return true;
                    break;
                }
            }
        } catch (Throwable ignored) {
        }
        try {
            Object devicePolicyManager = context.getSystemService("device_policy");
            Method activeAdmins = devicePolicyManager.getClass().getMethod("packageHasActiveAdmins", String.class);
            activeAdmins.setAccessible(true);
            Object value = activeAdmins.invoke(devicePolicyManager, packageName);
            if (value instanceof Boolean && ((Boolean)value)) return true;
        } catch (Throwable ignored) {
        }
        return false;
    }

    public static void main(String[] args) {
        try {
            if (Looper.myLooper() == null) Looper.prepareMainLooper();
            Context context = systemContext();
            PackageManager packageManager = context.getPackageManager();
            for (String packageName : args) {
                try {
                    ApplicationInfo info = packageManager.getApplicationInfo(packageName, 0);
                    CharSequence labelValue = packageManager.getApplicationLabel(info);
                    String label = labelValue == null ? "" : labelValue.toString().trim();
                    String encoded = Base64.encodeToString(label.getBytes(StandardCharsets.UTF_8), Base64.NO_WRAP);
                    System.out.println(packageName + "\t" + encoded + "\t" +
                        (isUninstallBlocked(context, packageName) ? "1" : "0"));
                } catch (Throwable packageError) {
                    System.out.println(packageName + "\t");
                }
            }
        } catch (Throwable error) {
            System.err.println("APP_LABEL_READER_ERROR: " + error);
            error.printStackTrace(System.err);
            if (error.getCause() != null) error.getCause().printStackTrace(System.err);
            System.exit(2);
        }
    }
}
