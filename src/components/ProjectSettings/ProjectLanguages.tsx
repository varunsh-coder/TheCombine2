import {
  Add,
  ArrowUpward,
  Clear,
  Delete,
  Done,
  Search,
} from "@mui/icons-material";
import {
  Grid,
  IconButton,
  MenuItem,
  Select,
  SelectChangeEvent,
  Typography,
} from "@mui/material";
import { LanguagePicker, languagePickerStrings_en } from "mui-language-picker";
import { Fragment, ReactElement, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "react-toastify";

import { WritingSystem } from "api/models";
import { getFrontierWords } from "backend";
import { IconButtonWithTooltip } from "components/Buttons";
import { ProjectSettingPropsWithUpdate } from "components/ProjectSettings/ProjectSettingsTypes";
import theme from "types/theme";
import { newWritingSystem, semDomWritingSystems } from "types/writingSystem";
import { getAnalysisLangsFromWords } from "utilities/wordUtilities";

const addAnalysisLangButtonId = "analysis-language-new";
const addAnalysisLangCleanButtonId = "analysis-language-new-clear";
const addAnalysisLangConfirmButtonId = "analysis-language-new-confirm";
const getProjAnalysisLangsButtonId = "analysis-language-get";
const semDomLangSelectId = "semantic-domains-language";

export default function ProjectLanguages(
  props: ProjectSettingPropsWithUpdate
): ReactElement {
  const [add, setAdd] = useState(false);
  const [isNewLang, setIsNewLang] = useState(false);
  const [langsInProj, setLangsInProj] = useState("");
  const [newLang, setNewLang] = useState(newWritingSystem());
  const { t } = useTranslation();

  useEffect(() => {
    setIsNewLang(
      !!newLang.bcp47 &&
        !props.project.analysisWritingSystems
          .map((ws) => ws.bcp47)
          .includes(newLang.bcp47)
    );
  }, [newLang.bcp47, props.project.analysisWritingSystems]);

  const setNewAnalysisDefault = async (index: number): Promise<void> => {
    const analysisWritingSystems = [...props.project.analysisWritingSystems];
    const newDefault = analysisWritingSystems.splice(index, 1)[0];
    analysisWritingSystems.splice(0, 0, newDefault);
    await props
      .updateProject({ ...props.project, analysisWritingSystems })
      .then(() => resetState())
      .catch((err) => {
        console.error(err);
        toast.error(
          t("projectSettings.language.makeDefaultAnalysisLanguageFailed")
        );
      });
  };

  const deleteAnalysisWritingSystem = async (index: number): Promise<void> => {
    const analysisWritingSystems = [...props.project.analysisWritingSystems];
    analysisWritingSystems.splice(index, 1);
    await props
      .updateProject({ ...props.project, analysisWritingSystems })
      .then(() => resetState())
      .catch((err) => {
        console.error(err);
        toast.error(t("projectSettings.language.deleteAnalysisLanguageFailed"));
      });
  };

  const addAnalysisWritingSystem = async (): Promise<void> => {
    if (!isNewLang) {
      toast.error(t("projectSettings.language.addAnalysisLanguageFailed"));
      return;
    }
    const analysisWritingSystems = [...props.project.analysisWritingSystems];
    analysisWritingSystems.push(newLang);
    await props
      .updateProject({ ...props.project, analysisWritingSystems })
      .then(() => resetState())
      .catch((err) => {
        console.error(err);
        toast.error(t("projectSettings.language.addAnalysisLanguageFailed"));
      });
  };

  const writingSystemButtons = (index: number): ReactElement => {
    return index === 0 || props.readOnly ? (
      // The top writing system is default and needs no buttons.
      <Fragment />
    ) : (
      <>
        <IconButtonWithTooltip
          icon={<ArrowUpward fontSize="inherit" />}
          textId="projectSettings.language.makeDefaultAnalysisLanguage"
          small
          onClick={() => setNewAnalysisDefault(index)}
          buttonId={`analysis-language-${index}-bump`}
        />
        <IconButtonWithTooltip
          icon={<Delete fontSize="inherit" />}
          textId="projectSettings.language.deleteAnalysisLanguage"
          small
          onClick={() => deleteAnalysisWritingSystem(index)}
          buttonId={`analysis-language-${index}-delete`}
        />
      </>
    );
  };

  const getActiveAnalysisLangs = async (): Promise<void> => {
    const langCodes = getAnalysisLangsFromWords(await getFrontierWords());
    langCodes.sort();
    setLangsInProj(langCodes.join(", "));
  };

  const setSemDomWritingSystem = async (lang: string): Promise<void> => {
    const semDomWritingSystem =
      semDomWritingSystems.find((ws) => ws.bcp47 === lang) ??
      newWritingSystem();
    await props
      .updateProject({ ...props.project, semDomWritingSystem })
      .then(() => resetState())
      .catch((err) => {
        console.error(err);
        toast.error(
          t("projectSettings.language.updateSemDomWritingSystemFailed")
        );
      });
  };

  const resetState = (): void => {
    setAdd(false);
    setLangsInProj("");
    setNewLang(newWritingSystem());
  };

  const addAnalysisLangButtons = (): ReactElement => (
    <>
      <IconButtonWithTooltip
        icon={<Add />}
        textId="projectSettings.language.addAnalysisLanguage"
        onClick={() => setAdd(true)}
        buttonId={addAnalysisLangButtonId}
      />
      <IconButtonWithTooltip
        icon={<Search />}
        textId="projectSettings.language.getGlossLanguages"
        onClick={() => getActiveAnalysisLangs()}
        buttonId={getProjAnalysisLangsButtonId}
      />
      {langsInProj}
    </>
  );

  const addAnalysisLangPicker = (): ReactElement => (
    <Grid container spacing={1} alignItems="center">
      <Grid item>
        <LanguagePicker
          value={newLang.name}
          setCode={(bcp47: string) =>
            setNewLang((prev: WritingSystem) => ({ ...prev, bcp47 }))
          }
          name={newLang.bcp47}
          setName={(name: string) =>
            setNewLang((prev: WritingSystem) => ({ ...prev, name }))
          }
          font={newLang.font}
          setFont={(font: string) =>
            setNewLang((prev: WritingSystem) => ({ ...prev, font }))
          }
          t={languagePickerStrings_en}
        />
      </Grid>{" "}
      <Grid item>
        <IconButton
          disabled={!isNewLang}
          onClick={() => addAnalysisWritingSystem()}
          id={addAnalysisLangConfirmButtonId}
          size="large"
        >
          <Done />
        </IconButton>
      </Grid>{" "}
      <Grid item>
        <IconButton
          onClick={() => resetState()}
          id={addAnalysisLangCleanButtonId}
          size="large"
        >
          <Clear />
        </IconButton>
      </Grid>
    </Grid>
  );

  const semDomLangSelect = (): ReactElement => (
    <Select
      variant="standard"
      id={semDomLangSelectId}
      value={props.project.semDomWritingSystem.bcp47}
      onChange={(event: SelectChangeEvent<string>) =>
        setSemDomWritingSystem(event.target.value as string)
      }
      /* Use `displayEmpty` and a conditional `renderValue` function to force
       * something to appear when the menu is closed and its value is "" */
      displayEmpty
      renderValue={
        props.project.semDomWritingSystem.bcp47
          ? undefined
          : () => t("projectSettings.language.semanticDomainsDefault")
      }
    >
      <MenuItem value={""}>
        {t("projectSettings.language.semanticDomainsDefault")}
      </MenuItem>
      {semDomWritingSystems.map((ws) => (
        <MenuItem key={ws.bcp47} value={ws.bcp47}>
          {`${ws.bcp47} (${ws.name})`}
        </MenuItem>
      ))}
    </Select>
  );

  return (
    <>
      {/* Vernacular language */}
      <Typography variant="h6">
        {t("projectSettings.language.vernacular")}
      </Typography>
      <ImmutableWritingSystem ws={props.project.vernacularWritingSystem} />

      {/* Analysis languages */}
      <Typography style={{ marginTop: theme.spacing(1) }} variant="h6">
        {t("projectSettings.language.analysis")}
      </Typography>
      {props.project.analysisWritingSystems.map((writingSystem, index) => (
        <ImmutableWritingSystem
          key={index}
          ws={writingSystem}
          index={index}
          buttons={writingSystemButtons(index)}
        />
      ))}
      {!props.readOnly &&
        (add ? addAnalysisLangPicker() : addAnalysisLangButtons())}

      {/* Semantic domains language */}
      <Typography style={{ marginTop: theme.spacing(1) }} variant="h6">
        {t("projectSettings.language.semanticDomains")}
      </Typography>
      {props.readOnly ? (
        props.project.semDomWritingSystem.bcp47 ? (
          <ImmutableWritingSystem ws={props.project.semDomWritingSystem} />
        ) : (
          <Typography>
            {t("projectSettings.language.semanticDomainsDefault")}
          </Typography>
        )
      ) : (
        semDomLangSelect()
      )}
    </>
  );
}

interface ImmutableWritingSystemProps {
  ws: WritingSystem;
  index?: number;
  buttons?: ReactElement;
}

function ImmutableWritingSystem(
  props: ImmutableWritingSystemProps
): ReactElement {
  const { t } = useTranslation();

  return (
    <Grid container spacing={1}>
      {props.index !== undefined && (
        <Grid item>
          <Typography>{`${props.index + 1}. `}</Typography>
        </Grid>
      )}
      <Grid item>
        {!!props.ws.name && (
          <Typography display="inline">
            {`${t("projectSettings.language.name")}: ${props.ws.name}, `}
          </Typography>
        )}
        <Typography display="inline">
          {`${t("projectSettings.language.bcp47")}: ${props.ws.bcp47}`}
        </Typography>
        {!!props.ws.font && (
          <Typography display="inline">
            {`, ${t("projectSettings.language.font")}: ${props.ws.font}`}
          </Typography>
        )}
      </Grid>
      <Grid item>{props.buttons}</Grid>
    </Grid>
  );
}
