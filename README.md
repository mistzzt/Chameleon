# Chameleon
账户系统行为替换方案

## 当前支持版本

TShock 版本: v4.3.24 (Api v2.1)

## 这个插件是干什么用的

注册登录对于从未进入Terraria服务器的玩家来说，是一个繁琐的过程。

在这个过程中，无数个玩家曾教过/看到别人教过新手如何注册登录 (`/register <密码> | /login <密码>`) 也曾因账户被注册而烦恼过。

为了解决这个问题，我利用了Terraria游戏里内建的输入密码功能来达到注册账户的目的。

## 效果

### 强制提示显示

> 未注册的玩家进服的时候，系统会提示配置文件里所设定的提示语。
>
> 玩家回到主界面后，再次加服输入密码即可进入游戏。

这个功能适合需要显示信息给新玩家的情况。

### 注册登录

> - 新玩家进服时，在`服务器密码`里输入自己的注册密码；以后进服就不需要。
> - 若玩家换电脑进入，则需要在`服务器密码`里输入自己注册时的密码，即可进服。

## **警告：在下列情况下请不要使用这个插件：**

- 当你的服务器有**设定密码**的时候

> 服务器有密码的情况下，就没法使用这个插件了。你可以选择禁用密码。
>
> 后续可能会做成支持的形式 （要求第一次进服的玩家输入两次密码）

- 当你的服务器不需要玩家登录的时候

> 不需要玩家注册登录那干嘛还要用这个插件？

### 如果你是上面的情况，而且你又用了这个插件的话：

- 服务器密码功能将失效
- 入服前登录将开启
- 注册登录将开启