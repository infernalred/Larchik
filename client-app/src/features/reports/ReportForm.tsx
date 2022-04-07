import axios from "axios";
import { Field, Form, Formik } from "formik";
import React, { useState } from "react";
import { toast } from "react-toastify";
import { Button, Segment } from "semantic-ui-react";
import MyDateInput from "../../app/common/form/MyDateInput";
import { ReportParams } from "../../app/models/reportParams";

export default function ReportForm() {
  const [loading, setLoading] = useState(false);

  const [params] = useState({
    startDate: new Date(new Date().getFullYear(), 0, 1),
    endDate: new Date()
  })


  function getFileNameFromContentDisposition(contentDisposition: any) {
    if (!contentDisposition) return null;

    const match = contentDisposition.match(/filename="?([^"]+)"?/);

    return match ? match[1] : null;
  }

  const handleDownload = async (params: ReportParams) => {
    console.log(params)
    setLoading(true);

    let res = null;

    try {
      res = await axios.get(`/reports/`, { params, responseType: 'blob' });
      setLoading(false);
    } catch (error) {
      setLoading(false);
      toast.error("Ошибка получения отчета");
      return;
    }

    const data = res.data;

    const url = window.URL.createObjectURL(
      new Blob([data], {
        type: res.headers["content-type"]
      })
    );

    const actualFileName = getFileNameFromContentDisposition(
      res.headers["content-disposition"]
    );

    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", actualFileName);
    document.body.appendChild(link);
    link.click();
    link.parentNode?.removeChild(link);
  };


  return (
    <Segment clearing>
      <Formik
        initialValues={params}
        onSubmit={values => handleDownload(values)}>
        {({ handleSubmit }) => (
          <Form className="ui form" onSubmit={handleSubmit} autoComplete="off" >
            <label>С даты</label>
            <MyDateInput placeholderText="С даты" name="startDate" />
            <label>По дату</label>
            <MyDateInput placeholderText="До даты" name="endDate" />
            <Button
              disabled={loading}
              loading={loading} floated='right'
              positive type='submit' content='Получить отчет' />
          </Form>
        )}
      </Formik>
    </Segment>
  )
}