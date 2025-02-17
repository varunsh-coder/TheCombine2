import { Email, Phone } from "@mui/icons-material";
import {
  Button,
  Card,
  CardContent,
  Grid,
  MenuItem,
  Select,
  TextField,
  Typography,
} from "@mui/material";
import { enqueueSnackbar } from "notistack";
import { FormEvent, Fragment, ReactElement, useState } from "react";
import { useTranslation } from "react-i18next";

import { User } from "api/models";
import { isEmailTaken, updateUser } from "backend";
import { getAvatar, getCurrentUser } from "backend/localStorage";
import ClickableAvatar from "components/UserSettings/ClickableAvatar";
import { updateLangFromUser } from "i18n";
import theme from "types/theme";
import { uiWritingSystems } from "types/writingSystem";

// Chrome silently converts non-ASCII characters in a Textfield of type="email".
// Use punycode.toUnicode() to convert them from punycode back to Unicode.
const punycode = require("punycode/");

export enum UserSettingsIds {
  ButtonSubmit = "user-settings-submit",
  FieldEmail = "user-settings-email",
  FieldName = "user-settings-name",
  FieldPhone = "user-settings-phone",
  FieldUsername = "user-settings-username",
  SelectUiLang = "user-settings-ui-lang",
}

export default (): ReactElement => {
  const potentialUser = getCurrentUser();
  return potentialUser ? <UserSettings user={potentialUser} /> : <Fragment />;
};

export function UserSettings(props: { user: User }): ReactElement {
  const [name, setName] = useState(props.user.name);
  const [phone, setPhone] = useState(props.user.phone);
  const [email, setEmail] = useState(props.user.email);
  const [uiLang, setUiLang] = useState(props.user.uiLang ?? "");
  const [emailTaken, setEmailTaken] = useState(false);
  const [avatar, setAvatar] = useState(getAvatar());

  const { t } = useTranslation();

  async function isEmailOkay(): Promise<boolean> {
    const unicodeEmail = punycode.toUnicode(email.toLowerCase());
    const unchanged = unicodeEmail === props.user.email.toLowerCase();
    return unchanged || !(await isEmailTaken(unicodeEmail));
  }

  const disabled =
    name === props.user.name &&
    phone === props.user.phone &&
    punycode.toUnicode(email) === props.user.email &&
    uiLang === (props.user.uiLang ?? "");

  async function onSubmit(e: FormEvent<HTMLFormElement>): Promise<void> {
    e.preventDefault();
    if (await isEmailOkay()) {
      await updateUser({
        ...props.user,
        name,
        phone,
        email: punycode.toUnicode(email),
        uiLang,
      });
      updateLangFromUser();
      enqueueSnackbar(t("userSettings.updateSuccess"));
    } else {
      setEmailTaken(true);
    }
  }

  return (
    <Grid container justifyContent="center">
      <Card style={{ width: 450 }}>
        <form onSubmit={(e) => onSubmit(e)}>
          <CardContent>
            <Grid item container spacing={6}>
              <Grid item container spacing={2} alignItems="center">
                <Grid item>
                  <ClickableAvatar avatar={avatar} setAvatar={setAvatar} />
                </Grid>
                <Grid item xs>
                  <TextField
                    id={UserSettingsIds.FieldName}
                    fullWidth
                    variant="outlined"
                    value={name}
                    label={t("login.name")}
                    onChange={(e) => setName(e.target.value)}
                    inputProps={{
                      "data-testid": UserSettingsIds.FieldName,
                      maxLength: 100,
                    }}
                    style={{ margin: theme.spacing(1), marginLeft: 0 }}
                  />
                  <Typography
                    data-testid={UserSettingsIds.FieldUsername}
                    id={UserSettingsIds.FieldUsername}
                    style={{ color: "grey" }}
                    variant="subtitle2"
                  >
                    {t("login.username")}
                    {": "}
                    {props.user.username}
                  </Typography>
                </Grid>
              </Grid>

              <Grid item container spacing={2}>
                <Grid item>
                  <Typography variant="h6">
                    {t("userSettings.contact")}
                  </Typography>
                </Grid>

                <Grid item container spacing={1} alignItems="center">
                  <Grid item>
                    <Phone />
                  </Grid>
                  <Grid item xs>
                    <TextField
                      id={UserSettingsIds.FieldPhone}
                      inputProps={{
                        "data-testid": UserSettingsIds.FieldPhone,
                      }}
                      fullWidth
                      variant="outlined"
                      value={phone}
                      label={t("userSettings.phone")}
                      onChange={(e) => setPhone(e.target.value)}
                      type="tel"
                    />
                  </Grid>
                </Grid>

                <Grid item container spacing={1} alignItems="center">
                  <Grid item>
                    <Email />
                  </Grid>
                  <Grid item xs>
                    <TextField
                      id={UserSettingsIds.FieldEmail}
                      inputProps={{
                        "data-testid": UserSettingsIds.FieldEmail,
                      }}
                      required
                      fullWidth
                      variant="outlined"
                      value={email}
                      label={t("login.email")}
                      onChange={(e) => {
                        setEmail(e.target.value);
                        setEmailTaken(false);
                      }}
                      error={emailTaken}
                      helperText={
                        emailTaken ? t("login.emailTaken") : undefined
                      }
                      type="email"
                    />
                  </Grid>
                </Grid>
              </Grid>

              <Grid item container spacing={2}>
                <Grid item xs={12}>
                  <Typography variant="h6">
                    {t("userSettings.uiLanguage")}
                  </Typography>
                </Grid>

                <Grid item>
                  <Select
                    variant="standard"
                    data-testid={UserSettingsIds.SelectUiLang}
                    id={UserSettingsIds.SelectUiLang}
                    value={uiLang}
                    onChange={(e) => setUiLang(e.target.value ?? "")}
                    /* Use `displayEmpty` and a conditional `renderValue` function to force
                     * something to appear when the menu is closed and its value is "" */
                    displayEmpty
                    renderValue={
                      uiLang
                        ? undefined
                        : () => t("userSettings.uiLanguageDefault")
                    }
                  >
                    <MenuItem value={""}>
                      {t("userSettings.uiLanguageDefault")}
                    </MenuItem>
                    {uiWritingSystems.map((ws) => (
                      <MenuItem key={ws.bcp47} value={ws.bcp47}>
                        {`${ws.bcp47} (${ws.name})`}
                      </MenuItem>
                    ))}
                  </Select>
                </Grid>
              </Grid>

              <Grid item container justifyContent="flex-end">
                <Button
                  data-testid={UserSettingsIds.ButtonSubmit}
                  disabled={disabled}
                  id={UserSettingsIds.ButtonSubmit}
                  type="submit"
                  variant="contained"
                >
                  {t("buttons.save")}
                </Button>
              </Grid>
            </Grid>
          </CardContent>
        </form>
      </Card>
    </Grid>
  );
}
